using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;

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
        var jiraStatusCacheByTask = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
                        jiraStatusCacheByTask,
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
        ConcurrentDictionary<string, string> jiraStatusCacheByTask,
        SemaphoreSlim jiraSemaphore,
        BitbucketProgressCallbacks? progress,
        CancellationToken cancellationToken)
    {
        var tags = new List<(string Name, string? CommitHash)>();

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
                return RepositoryTagLookup.RepoNotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var details = string.IsNullOrWhiteSpace(body)
                    ? $"{(int)response.StatusCode} {response.ReasonPhrase}"
                    : $"{(int)response.StatusCode} {response.ReasonPhrase}: {TrimText(body, 180)}";

                return RepositoryTagLookup.ApiError(details);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var page = await JsonSerializer.DeserializeAsync<TagPageDto>(
                stream,
                _jsonSerializerOptions,
                cancellationToken).ConfigureAwait(false);

            if (page?.Values is not null)
            {
                foreach (var tag in page.Values)
                {
                    if (!string.IsNullOrWhiteSpace(tag.Name))
                    {
                        tags.Add((tag.Name.Trim(), tag.Target?.Hash?.Trim()));
                    }
                }
            }

            next = CreateUriOrNull(page?.Next);
        }

        var distinctTags = tags
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();

        var tagsToInspect = distinctTags
            .Where(tag => minCurrentVersion is null
                || (VersionParser.TryParse(tag.Name, out var parsedVersion) && parsedVersion > minCurrentVersion))
            .ToArray();

        progress?.CommitTotalDetected?.Invoke(repository.Value, tagsToInspect.Length);

        var jiraCacheByCommit = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var enrichedTags = new List<RepositoryTagInfo>(tagsToInspect.Length);

        foreach (var (name, commitHash) in tagsToInspect)
        {
            var jiraTask = "N/A";
            var jiraStatus = "N/A";

            if (!string.IsNullOrWhiteSpace(commitHash))
            {
                if (!jiraCacheByCommit.TryGetValue(commitHash, out jiraTask))
                {
                    var message = await TryGetCommitMessageAsync(
                        httpClient,
                        options,
                        repository,
                        commitHash,
                        cancellationToken).ConfigureAwait(false);

                    jiraTask = ExtractJiraTask(message, options.ProjectName);
                    jiraCacheByCommit[commitHash] = jiraTask;
                }
            }

            jiraStatus = await ResolveJiraStatusAsync(
                jiraHttpClient,
                jiraOptions,
                jiraTask,
                jiraStatusCacheByTask,
                jiraSemaphore,
                cancellationToken).ConfigureAwait(false);

            enrichedTags.Add(new RepositoryTagInfo(name, jiraTask, jiraStatus));
            progress?.CommitProcessed?.Invoke(repository.Value);
        }

        return RepositoryTagLookup.Success(enrichedTags);
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

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var commit = await JsonSerializer.DeserializeAsync<CommitDto>(
            stream,
            _jsonSerializerOptions,
            cancellationToken).ConfigureAwait(false);

        return commit?.Message;
    }

    private static string ExtractJiraTask(string? commitMessage, string projectName)
    {
        if (string.IsNullOrWhiteSpace(commitMessage) || string.IsNullOrWhiteSpace(projectName))
        {
            return "N/A";
        }

        var pattern = $@"\b{Regex.Escape(projectName)}-\d+\b";
        var matches = Regex.Matches(commitMessage, pattern, RegexOptions.IgnoreCase);

        if (matches.Count == 0)
        {
            return "N/A";
        }

        var jiraTasks = matches
            .Select(x => x.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join(", ", jiraTasks);
    }

    private static async Task<string> ResolveJiraStatusAsync(
        HttpClient jiraHttpClient,
        JiraOptions jiraOptions,
        string jiraTask,
        ConcurrentDictionary<string, string> jiraStatusCacheByTask,
        SemaphoreSlim jiraSemaphore,
        CancellationToken cancellationToken)
    {
        var jiraTasks = SplitJiraTasks(jiraTask);
        if (jiraTasks.Length == 0)
        {
            return "N/A";
        }

        var jiraStatuses = new List<string>(jiraTasks.Length);
        foreach (var task in jiraTasks)
        {
            if (!jiraStatusCacheByTask.TryGetValue(task, out var jiraStatus))
            {
                await jiraSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    jiraStatus = await TryGetJiraStatusAsync(
                        jiraHttpClient,
                        jiraOptions,
                        task,
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _ = jiraSemaphore.Release();
                }

                if (!IsTransientJiraStatus(jiraStatus))
                {
                    _ = jiraStatusCacheByTask.TryAdd(task, jiraStatus);
                }
            }

            jiraStatuses.Add(jiraStatus);
        }

        return string.Join(", ", jiraStatuses);
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

    private static async Task<string> TryGetJiraStatusAsync(
        HttpClient jiraHttpClient,
        JiraOptions jiraOptions,
        string jiraTask,
        CancellationToken cancellationToken)
    {
        var issueUrl = new Uri($"rest/api/3/issue/{Uri.EscapeDataString(jiraTask)}?expand=changelog", UriKind.Relative);

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

            return statusFromSearch ?? "Not found";
        }

        if (!response.IsSuccessStatusCode)
        {
            return $"HTTP {(int)response.StatusCode}";
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var issue = await JsonSerializer.DeserializeAsync<JiraIssueStatusResponseDto>(
            stream,
            _jsonSerializerOptions,
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(issue?.Fields?.Status?.Name))
        {
            return issue.Fields.Status.Name.Trim();
        }

        return "N/A";
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

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var searchResult = await JsonSerializer.DeserializeAsync<JiraSearchResponseDto>(
            stream,
            _jsonSerializerOptions,
            cancellationToken).ConfigureAwait(false);

        var status = searchResult is not null && searchResult.Issues.Count > 0
            ? searchResult.Issues[0].Fields?.Status?.Name
            : null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            return status.Trim();
        }

        return "Not found";
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

    private static Uri? CreateUriOrNull(string? next)
    {
        if (string.IsNullOrWhiteSpace(next))
        {
            return null;
        }

        if (Uri.TryCreate(next, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        if (Uri.TryCreate(next, UriKind.Relative, out var relative))
        {
            return relative;
        }

        return null;
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

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BitbucketOptions _bitbucketOptions;
    private readonly JiraOptions _jiraOptions;
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
