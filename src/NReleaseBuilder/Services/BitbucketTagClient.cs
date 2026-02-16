using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;
using NReleaseBuilder.Transport;

using Microsoft.Extensions.Options;

using NuGet.Versioning;

namespace NReleaseBuilder.Services;

/// <summary>
/// Bitbucket/Jira data loader for repository tags and Jira status enrichment.
/// </summary>
public sealed class BitbucketTagClient : IBitbucketTagClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketTagClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="options">Application settings options.</param>
    public BitbucketTagClient(IHttpClientFactory httpClientFactory, IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);

        var appSettings = options.Value;
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(appSettings.Bitbucket);
        ArgumentNullException.ThrowIfNull(appSettings.Jira);

        _httpClientFactory = httpClientFactory;
        _bitbucketOptions = appSettings.Bitbucket;
        _jiraOptions = appSettings.Jira;
    }

    /// <inheritdoc />
    public async Task<Dictionary<RepositoryName, RepositoryTagLookup>> FetchRepositoryTagLookupsAsync(
        IReadOnlyList<RepositoryName> repositories,
        IReadOnlyDictionary<RepositoryName, NuGetVersion> minCurrentVersionsByRepository,
        BitbucketProgressCallbacks? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(minCurrentVersionsByRepository);

        var httpClient = _httpClientFactory.CreateClient(HttpClientNames.BITBUCKET);
        var jiraHttpClient = _httpClientFactory.CreateClient(HttpClientNames.JIRA);
        using var semaphore = new SemaphoreSlim(
            _bitbucketOptions.MaxParallelRequests,
            _bitbucketOptions.MaxParallelRequests);
        using var jiraSemaphore = new SemaphoreSlim(
            _jiraOptions.MaxParallelRequests,
            _jiraOptions.MaxParallelRequests);
        var jiraTaskInfoCacheByTask = new ConcurrentDictionary<string, JiraTaskInfo>(StringComparer.OrdinalIgnoreCase);

        var tasks = repositories.Select(async repository =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                progress?.RepositoryStarted?.Invoke(repository.Value);

                _ = minCurrentVersionsByRepository.TryGetValue(repository, out var minCurrentVersion);
                var lookup = await GetRepositoryTagLookupAsync(
                        httpClient,
                        jiraHttpClient,
                        _bitbucketOptions,
                        _jiraOptions,
                        repository,
                        minCurrentVersion,
                        jiraTaskInfoCacheByTask,
                        jiraSemaphore,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);

                return (Repository: repository, Lookup: lookup);
            }
            finally
            {
                progress?.RepositoryCompleted?.Invoke(repository.Value);
                _ = semaphore.Release();
            }
        }).ToArray();

        var pairs = await Task.WhenAll(tasks).ConfigureAwait(false);
        return pairs.ToDictionary(x => x.Repository, x => x.Lookup);
    }

    private static async Task<RepositoryTagLookup> GetRepositoryTagLookupAsync(
        HttpClient httpClient,
        HttpClient jiraHttpClient,
        BitbucketOptions options,
        JiraOptions jiraOptions,
        RepositoryName repository,
        NuGetVersion? minCurrentVersion,
        ConcurrentDictionary<string, JiraTaskInfo> jiraTaskInfoCacheByTask,
        SemaphoreSlim jiraSemaphore,
        BitbucketProgressCallbacks? progress,
        CancellationToken cancellationToken)
    {
        var repositoryForBitbucketCalls = options.ResolveRepositoryName(repository);
        var tagLoadResult = await LoadRepositoryTagReferencesAsync(
                httpClient,
                options,
                repositoryForBitbucketCalls,
                cancellationToken)
            .ConfigureAwait(false);

        if (tagLoadResult.IsRepositoryMissing
            && options.UseTruncatedRepositoryNameFallback
            && TryBuildTruncatedRepositoryName(repositoryForBitbucketCalls) is { } truncatedRepository)
        {
            var fallbackLoadResult = await LoadRepositoryTagReferencesAsync(
                    httpClient,
                    options,
                    truncatedRepository,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!fallbackLoadResult.IsRepositoryMissing)
            {
                repositoryForBitbucketCalls = truncatedRepository;
                tagLoadResult = fallbackLoadResult;
            }
        }

        if (tagLoadResult.IsRepositoryMissing)
        {
            return RepositoryTagLookup.RepoNotFound(repositoryForBitbucketCalls);
        }

        if (!string.IsNullOrWhiteSpace(tagLoadResult.Error))
        {
            return RepositoryTagLookup.ApiError(repositoryForBitbucketCalls, tagLoadResult.Error);
        }

        var projectNames = options.ResolveProjectNames();

        var tagsToInspect = tagLoadResult.Tags
            .Where(tag => minCurrentVersion is null
                || (VersionParser.TryParse(tag.Name, out var parsedVersion) && parsedVersion > minCurrentVersion))
            .ToArray();

        progress?.CommitTotalDetected?.Invoke(repository.Value, tagsToInspect.Length);

        var jiraCacheByCommit = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var enrichedTags = new List<RepositoryTagInfo>(tagsToInspect.Length);

        foreach (var tag in tagsToInspect)
        {
            var jiraTask = "N/A";
            var jiraTitle = "N/A";
            var jiraStatus = "N/A";
            var commitHash = tag.CommitHash;

            if (!string.IsNullOrWhiteSpace(commitHash))
            {
                if (!jiraCacheByCommit.TryGetValue(commitHash, out jiraTask))
                {
                    var message = await TryGetCommitMessageAsync(
                        httpClient,
                        options,
                        repositoryForBitbucketCalls,
                        commitHash,
                        cancellationToken).ConfigureAwait(false);

                    jiraTask = ExtractJiraTask(message, projectNames);
                    jiraCacheByCommit[commitHash] = jiraTask;
                }
            }

            var jiraResolution = await ResolveJiraTaskResolutionAsync(
                jiraHttpClient,
                jiraOptions,
                jiraTask,
                projectNames,
                jiraTaskInfoCacheByTask,
                jiraSemaphore,
                cancellationToken).ConfigureAwait(false);

            jiraStatus = jiraResolution.Statuses;
            jiraTask = jiraResolution.Tasks;
            jiraTitle = jiraResolution.Titles;

            enrichedTags.Add(new RepositoryTagInfo(
                new VersionLabel(tag.Name),
                new JiraTaskReference(jiraTask),
                new JiraTitleReference(jiraTitle),
                new JiraStatusReference(jiraStatus),
                jiraResolution.TaskAlertDetails,
                jiraResolution.HasRequiredActions,
                jiraResolution.HasBreakingChanges,
                jiraResolution.HasDependencyIssues));
            progress?.CommitProcessed?.Invoke(repository.Value);
        }

        return RepositoryTagLookup.Success(repositoryForBitbucketCalls, enrichedTags);
    }

    private static async Task<RepositoryTagReferenceLoadResult> LoadRepositoryTagReferencesAsync(
        HttpClient httpClient,
        BitbucketOptions options,
        RepositoryName repository,
        CancellationToken cancellationToken)
    {
        var tags = new List<RepositoryTagReference>();

        Uri? next = new(
            $"repositories/{Uri.EscapeDataString(options.Workspace)}/{Uri.EscapeDataString(repository.Value)}/refs/tags?pagelen={options.PageLen}",
            UriKind.Relative);

        while (next is not null)
        {
            using var response = await GetWithRetryAsync(
                httpClient,
                next,
                options.RetryCount,
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return RepositoryTagReferenceLoadResult.RepoNotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var details = string.IsNullOrWhiteSpace(body)
                    ? $"{(int)response.StatusCode} {response.ReasonPhrase}"
                    : $"{(int)response.StatusCode} {response.ReasonPhrase}: {TrimText(body, 180)}";

                return RepositoryTagReferenceLoadResult.ApiError(details);
            }

            var page = await ReadTagPageAsync(response, cancellationToken).ConfigureAwait(false);
            tags.AddRange(page.Values);
            next = page.Next;
        }

        var distinctTags = tags
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();

        return RepositoryTagReferenceLoadResult.Success(distinctTags);
    }

    private static RepositoryName? TryBuildTruncatedRepositoryName(RepositoryName repository)
    {
        var value = repository.Value;
        var lastDotIndex = value.LastIndexOf('.');

        if (lastDotIndex <= 0 || lastDotIndex == value.Length - 1)
        {
            return null;
        }

        return new RepositoryName(value[..lastDotIndex]);
    }

    private static async Task<string?> TryGetCommitMessageAsync(
        HttpClient httpClient,
        BitbucketOptions options,
        RepositoryName repository,
        string commitHash,
        CancellationToken cancellationToken)
    {
        var commitUrl = new Uri(
            $"repositories/{Uri.EscapeDataString(options.Workspace)}/{Uri.EscapeDataString(repository.Value)}/commit/{Uri.EscapeDataString(commitHash)}",
            UriKind.Relative);

        using var response = await GetWithRetryAsync(
            httpClient,
            commitUrl,
            options.RetryCount,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var commit = await ReadCommitInfoAsync(response, cancellationToken).ConfigureAwait(false);
        return commit?.Message;
    }

    private static string ExtractJiraTask(string? commitMessage, string[] projectNames)
    {
        if (string.IsNullOrWhiteSpace(commitMessage) || projectNames.Length == 0)
        {
            return "N/A";
        }

        var projectNamePattern = string.Join(
            "|",
            projectNames.Select(static projectName => Regex.Escape(projectName)));
        var pattern = $@"\b(?<project>{projectNamePattern})-\d+\b";
        var matches = Regex.Matches(commitMessage, pattern, RegexOptions.IgnoreCase);

        if (matches.Count == 0)
        {
            return "N/A";
        }

        var selectedProjectName = matches[0].Groups["project"].Value;

        var jiraTasks = matches
            .Where(match => string.Equals(
                match.Groups["project"].Value,
                selectedProjectName,
                StringComparison.OrdinalIgnoreCase))
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join(", ", jiraTasks);
    }

    private static async Task<JiraTaskResolution> ResolveJiraTaskResolutionAsync(
        HttpClient jiraHttpClient,
        JiraOptions jiraOptions,
        string jiraTask,
        string[] projectNames,
        ConcurrentDictionary<string, JiraTaskInfo> jiraTaskInfoCacheByTask,
        SemaphoreSlim jiraSemaphore,
        CancellationToken cancellationToken)
    {
        var jiraTasks = SplitJiraTasks(jiraTask);
        if (jiraTasks.Length == 0)
        {
            return new JiraTaskResolution("N/A", jiraTask, "N/A", [], false, false, false);
        }

        var jiraStatuses = new List<string>(jiraTasks.Length);
        var jiraTitles = new List<string>(jiraTasks.Length);
        var taskAlertDetails = new List<JiraTaskAlertDetails>(jiraTasks.Length);
        var hasRequiredActions = false;
        var hasBreakingChanges = false;
        var hasDependencyIssues = false;
        foreach (var task in jiraTasks)
        {
            if (!jiraTaskInfoCacheByTask.TryGetValue(task, out var jiraTaskInfo))
            {
                await jiraSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    jiraTaskInfo = await TryGetJiraTaskInfoAsync(
                        jiraHttpClient,
                        jiraOptions,
                        task,
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _ = jiraSemaphore.Release();
                }

                if (!IsTransientJiraStatus(jiraTaskInfo.Status))
                {
                    _ = jiraTaskInfoCacheByTask.TryAdd(task, jiraTaskInfo);
                }
            }

            jiraStatuses.Add(jiraTaskInfo.Status);
            jiraTitles.Add(jiraTaskInfo.Title);
            var requiredActionsDetails = jiraOptions.CheckReleaseAlerts
                ? jiraTaskInfo.RequiredActionsDetails
                : null;
            var breakingChangesDetails = jiraOptions.CheckReleaseAlerts
                ? jiraTaskInfo.BreakingChangesDetails
                : null;
            taskAlertDetails.Add(new JiraTaskAlertDetails(
                new JiraTaskReference(task),
                new JiraTitleReference(jiraTaskInfo.Title),
                new JiraStatusReference(jiraTaskInfo.Status),
                requiredActionsDetails,
                breakingChangesDetails));
            if (jiraOptions.CheckReleaseAlerts)
            {
                hasRequiredActions |= jiraTaskInfo.HasRequiredActions;
                hasBreakingChanges |= jiraTaskInfo.HasBreakingChanges;
                hasDependencyIssues |= HasDependencyIssue(
                    task,
                    jiraTaskInfo.RequiredActionsDetails,
                    jiraTaskInfo.BreakingChangesDetails,
                    projectNames);
            }
        }

        return new JiraTaskResolution(
            string.Join(", ", jiraStatuses),
            string.Join(", ", jiraTasks),
            string.Join(", ", jiraTitles),
            taskAlertDetails,
            hasRequiredActions,
            hasBreakingChanges,
            hasDependencyIssues);
    }

    private static string[] SplitJiraTasks(string jiraTask)
    {
        if (string.IsNullOrWhiteSpace(jiraTask) || string.Equals(jiraTask, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return
        [
            .. jiraTask
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static async Task<JiraTaskInfo> TryGetJiraTaskInfoAsync(
        HttpClient jiraHttpClient,
        JiraOptions jiraOptions,
        string jiraTask,
        CancellationToken cancellationToken)
    {
        var issueUrl = new Uri($"rest/api/3/issue/{Uri.EscapeDataString(jiraTask)}?expand=names", UriKind.Relative);

        using var response = await GetWithRetryAsync(
            jiraHttpClient,
            issueUrl,
            jiraOptions.RetryCount,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var statusFromSearch = await TryGetJiraStatusBySearchAsync(
                jiraHttpClient,
                jiraOptions,
                jiraTask,
                cancellationToken).ConfigureAwait(false);

            return new JiraTaskInfo(statusFromSearch ?? "Not found", "N/A", null, null);
        }

        if (!response.IsSuccessStatusCode)
        {
            return new JiraTaskInfo($"HTTP {(int)response.StatusCode}", "N/A", null, null);
        }

        var issue = await ReadJiraIssueInfoAsync(response, jiraOptions, cancellationToken).ConfigureAwait(false);
        var status = issue?.StatusName?.Value ?? "N/A";
        var title = issue?.Title ?? "N/A";
        var requiredActionsDetails = issue?.RequiredActionsDetails;
        var breakingChangesDetails = issue?.BreakingChangesDetails;

        return new JiraTaskInfo(status, title, requiredActionsDetails, breakingChangesDetails);
    }

    private static async Task<string?> TryGetJiraStatusBySearchAsync(
        HttpClient jiraHttpClient,
        JiraOptions jiraOptions,
        string jiraTask,
        CancellationToken cancellationToken)
    {
        var jql = $"key = \"{jiraTask}\"";

        var api3SearchUrl = new Uri(
            $"rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}&fields=status&maxResults=1",
            UriKind.Relative);

        var api3Result = await TryGetJiraStatusBySearchUrlAsync(
            jiraHttpClient,
            jiraOptions,
            api3SearchUrl,
            cancellationToken).ConfigureAwait(false);
        if (api3Result is not null)
        {
            return api3Result;
        }

        // Some Jira instances expose only v2 search endpoint.
        var api2SearchUrl = new Uri(
            $"rest/api/2/search?jql={Uri.EscapeDataString(jql)}&fields=status&maxResults=1",
            UriKind.Relative);

        return await TryGetJiraStatusBySearchUrlAsync(
            jiraHttpClient,
            jiraOptions,
            api2SearchUrl,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> TryGetJiraStatusBySearchUrlAsync(
        HttpClient jiraHttpClient,
        JiraOptions jiraOptions,
        Uri searchUrl,
        CancellationToken cancellationToken)
    {
        using var response = await GetWithRetryAsync(
            jiraHttpClient,
            searchUrl,
            jiraOptions.RetryCount,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return $"HTTP {(int)response.StatusCode}";
        }

        var searchResult = await ReadJiraSearchResultAsync(response, cancellationToken).ConfigureAwait(false);

        var status = searchResult is not null && searchResult.Issues.Count > 0
            ? searchResult.Issues[0].StatusName
            : null;

        if (status is { } jiraStatus)
        {
            return jiraStatus.Value;
        }

        return "Not found";
    }

    private static async Task<RepositoryTagPage> ReadTagPageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var pageDto = await JsonSerializer.DeserializeAsync<TagPageDto>(
            stream,
            _jsonSerializerOptions,
            cancellationToken).ConfigureAwait(false);

        return pageDto is null ? RepositoryTagPage.Empty : pageDto.ToDomain();
    }

    private static async Task<CommitInfo?> ReadCommitInfoAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var commitDto = await JsonSerializer.DeserializeAsync<CommitDto>(
            stream,
            _jsonSerializerOptions,
            cancellationToken).ConfigureAwait(false);

        return commitDto?.ToDomain();
    }

    private static async Task<JiraIssueInfo?> ReadJiraIssueInfoAsync(
        HttpResponseMessage response,
        JiraOptions jiraOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jiraOptions);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var issueDto = await JsonSerializer.DeserializeAsync<JiraIssueStatusResponseDto>(
            stream,
            _jsonSerializerOptions,
            cancellationToken).ConfigureAwait(false);

        return issueDto?.ToDomain(
            jiraOptions.RequiredActionsFieldName,
            jiraOptions.BreakingChangesFieldName);
    }

    private static async Task<JiraSearchResult?> ReadJiraSearchResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var searchDto = await JsonSerializer.DeserializeAsync<JiraSearchResponseDto>(
            stream,
            _jsonSerializerOptions,
            cancellationToken).ConfigureAwait(false);

        return searchDto?.ToDomain();
    }

    private static bool IsTransientJiraStatus(string jiraStatus)
    {
        return jiraStatus is "HTTP 408"
            or "HTTP 429"
            or "HTTP 500"
            or "HTTP 502"
            or "HTTP 503"
            or "HTTP 504";
    }

    private static async Task<HttpResponseMessage> GetWithRetryAsync(
        HttpClient httpClient,
        Uri url,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (attempt < retryCount && IsTransientStatusCode(response.StatusCode))
                {
                    var delay = GetRetryDelay(attempt + 1, response.Headers.RetryAfter);
                    response.Dispose();
                    attempt++;
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return response;
            }
            catch (HttpRequestException) when (attempt < retryCount)
            {
                attempt++;
                await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    private static TimeSpan GetRetryDelay(int attempt, RetryConditionHeaderValue? retryAfter = null)
    {
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return CapRetryDelay(delta);
        }

        if (retryAfter?.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                return CapRetryDelay(remaining);
            }
        }

        var milliseconds = Math.Min(10000, 300 * Math.Pow(2, attempt - 1));
        milliseconds += RandomNumberGenerator.GetInt32(100, 400);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static TimeSpan CapRetryDelay(TimeSpan value)
    {
        var max = TimeSpan.FromSeconds(30);
        return value > max ? max : value;
    }

    private static string TrimText(string value, int maxLength)
    {
        var oneLine = value.ReplaceLineEndings(" ").Trim();
        if (oneLine.Length <= maxLength)
        {
            return oneLine;
        }

        return oneLine[..maxLength] + "...";
    }

    private static bool HasDependencyIssue(
        string currentTask,
        string? requiredActionsDetails,
        string? breakingChangesDetails,
        string[] projectNames)
    {
        if (projectNames.Length == 0)
        {
            return false;
        }

        var details = string.Join(
            Environment.NewLine,
            new[] { requiredActionsDetails, breakingChangesDetails }
                .Where(static text => !string.IsNullOrWhiteSpace(text)));

        if (string.IsNullOrWhiteSpace(details))
        {
            return false;
        }

        var projectNamePattern = string.Join(
            "|",
            projectNames.Select(static projectName => Regex.Escape(projectName)));
        if (string.IsNullOrWhiteSpace(projectNamePattern))
        {
            return false;
        }

        var matches = Regex.Matches(details, $@"\b(?:{projectNamePattern})-\d+\b", RegexOptions.IgnoreCase);
        if (matches.Count == 0)
        {
            return false;
        }

        foreach (Match match in matches)
        {
            var taskReference = match.Value.ToUpperInvariant();
            if (!string.Equals(taskReference, currentTask, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct JiraTaskResolution(
        string Statuses,
        string Tasks,
        string Titles,
        IReadOnlyList<JiraTaskAlertDetails> TaskAlertDetails,
        bool HasRequiredActions,
        bool HasBreakingChanges,
        bool HasDependencyIssues);

    private readonly record struct JiraTaskInfo(
        string Status,
        string Title,
        string? RequiredActionsDetails,
        string? BreakingChangesDetails)
    {
        public bool HasRequiredActions => !string.IsNullOrWhiteSpace(RequiredActionsDetails);

        public bool HasBreakingChanges => !string.IsNullOrWhiteSpace(BreakingChangesDetails);
    }

    private readonly record struct RepositoryTagReferenceLoadResult(
        bool IsRepositoryMissing,
        string? Error,
        RepositoryTagReference[] Tags)
    {
        public static RepositoryTagReferenceLoadResult RepoNotFound() => new(true, null, []);

        public static RepositoryTagReferenceLoadResult ApiError(string error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);
            return new RepositoryTagReferenceLoadResult(false, error, []);
        }

        public static RepositoryTagReferenceLoadResult Success(RepositoryTagReference[] tags)
        {
            ArgumentNullException.ThrowIfNull(tags);
            return new RepositoryTagReferenceLoadResult(false, null, tags);
        }
    }

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BitbucketOptions _bitbucketOptions;
    private readonly JiraOptions _jiraOptions;
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
