using System.Collections.Concurrent;
using System.Net;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Jira.Internal.Models;
using NReleaseBuilder.Models;
using NReleaseBuilder.Transport;
using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Jira;

/// <summary>
/// Jira task resolver that extracts task keys from commit messages and loads Jira metadata.
/// </summary>
public sealed class JiraTaskResolver : IJiraTaskResolver, IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraTaskResolver"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="options">Application settings options.</param>
    /// <param name="jiraParser">Jira parser abstraction.</param>
    /// <param name="httpRetryExecutor">Shared HTTP retry executor.</param>
    /// <param name="responseSerializer">HTTP response serializer.</param>
    public JiraTaskResolver(
        IHttpClientFactory httpClientFactory,
        IOptions<AppSettings> options,
        IJiraParser jiraParser,
        IHttpRetryExecutor httpRetryExecutor,
        IResponseSerializer responseSerializer)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(jiraParser);
        ArgumentNullException.ThrowIfNull(httpRetryExecutor);
        ArgumentNullException.ThrowIfNull(responseSerializer);

        var appSettings = options.Value;

        _httpClientFactory = httpClientFactory;
        _jiraParser = jiraParser;
        _httpRetryExecutor = httpRetryExecutor;
        _responseSerializer = responseSerializer;
        _jiraOptions = appSettings.Jira;
        _jiraSemaphore = new SemaphoreSlim(
            _jiraOptions.MaxParallelRequests,
            _jiraOptions.MaxParallelRequests);
    }

    /// <inheritdoc />
    public async Task<JiraTaskResolution> ResolveFromCommitMessageAsync(
        string? commitMessage,
        IReadOnlyList<string> projectNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectNames);

        var jiraHttpClient = _httpClientFactory.CreateClient(HttpClientNames.JIRA);
        var jiraTask = _jiraParser.ExtractJiraTask(commitMessage, projectNames);
        return await ResolveJiraTaskResolutionAsync(
            jiraHttpClient,
            jiraTask,
            projectNames,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => _jiraSemaphore.Dispose();

    private async Task<JiraTaskResolution> ResolveJiraTaskResolutionAsync(
        HttpClient jiraHttpClient,
        string jiraTask,
        IReadOnlyList<string> projectNames,
        CancellationToken cancellationToken)
    {
        var jiraTasks = _jiraParser.SplitJiraTasks(jiraTask);
        if (jiraTasks.Length == 0)
        {
            return JiraTaskResolution.NotAvailable(jiraTask);
        }

        var jiraStatuses = new List<string>(jiraTasks.Length);
        var jiraTitles = new List<string>(jiraTasks.Length);
        var taskAlertDetails = new List<JiraTaskAlertDetails>(jiraTasks.Length);
        var hasRequiredActions = false;
        var hasBreakingChanges = false;
        var hasDependencyIssues = false;

        foreach (var task in jiraTasks)
        {
            if (!_jiraTaskInfoCacheByTask.TryGetValue(task, out var jiraTaskInfo))
            {
                await _jiraSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    jiraTaskInfo = await TryGetJiraTaskInfoAsync(
                        jiraHttpClient,
                        task,
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _ = _jiraSemaphore.Release();
                }

                if (!jiraTaskInfo.IsTransientStatus())
                {
                    _ = _jiraTaskInfoCacheByTask.TryAdd(task, jiraTaskInfo);
                }
            }

            jiraStatuses.Add(jiraTaskInfo.Status);
            jiraTitles.Add(jiraTaskInfo.Title);
            var requiredActionsDetails = _jiraOptions.CheckReleaseAlerts
                ? jiraTaskInfo.RequiredActionsDetails
                : null;
            var breakingChangesDetails = _jiraOptions.CheckReleaseAlerts
                ? jiraTaskInfo.BreakingChangesDetails
                : null;

            taskAlertDetails.Add(new JiraTaskAlertDetails(
                new JiraTaskReference(task),
                new JiraTitleReference(jiraTaskInfo.Title),
                new JiraStatusReference(jiraTaskInfo.Status),
                requiredActionsDetails,
                breakingChangesDetails));

            if (_jiraOptions.CheckReleaseAlerts)
            {
                hasRequiredActions |= jiraTaskInfo.HasRequiredActions;
                hasBreakingChanges |= jiraTaskInfo.HasBreakingChanges;
                hasDependencyIssues |= _jiraParser.HasDependencyIssue(
                    task,
                    jiraTaskInfo.RequiredActionsDetails,
                    jiraTaskInfo.BreakingChangesDetails,
                    projectNames);
            }
        }

        return new JiraTaskResolution(
            jiraStatuses,
            jiraTasks,
            jiraTitles,
            taskAlertDetails,
            hasRequiredActions,
            hasBreakingChanges,
            hasDependencyIssues);
    }

    private async Task<JiraTaskInfo> TryGetJiraTaskInfoAsync(
        HttpClient jiraHttpClient,
        string jiraTask,
        CancellationToken cancellationToken)
    {
        var issueUrl = new Uri($"rest/api/3/issue/{Uri.EscapeDataString(jiraTask)}?expand=names", UriKind.Relative);

        using var response = await _httpRetryExecutor.GetAsync(
            jiraHttpClient,
            issueUrl,
            _jiraOptions.RetryCount,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var statusFromSearch = await TryGetJiraStatusBySearchAsync(
                jiraHttpClient,
                jiraTask,
                cancellationToken).ConfigureAwait(false);

            return JiraTaskInfo.NotFound(statusFromSearch);
        }

        if (!response.IsSuccessStatusCode)
        {
            return JiraTaskInfo.HttpError(response.StatusCode);
        }

        var issue = await ReadJiraIssueInfoAsync(response, cancellationToken).ConfigureAwait(false);
        var status = issue?.StatusName?.Value ?? "N/A";
        var title = issue?.Title ?? "N/A";
        var requiredActionsDetails = issue?.RequiredActionsDetails;
        var breakingChangesDetails = issue?.BreakingChangesDetails;

        return new JiraTaskInfo(status, title, requiredActionsDetails, breakingChangesDetails);
    }

    private async Task<string?> TryGetJiraStatusBySearchAsync(
        HttpClient jiraHttpClient,
        string jiraTask,
        CancellationToken cancellationToken)
    {
        var jql = $"key = \"{jiraTask}\"";

        var api3SearchUrl = new Uri(
            $"rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}&fields=status&maxResults=1",
            UriKind.Relative);

        var api3Result = await TryGetJiraStatusBySearchUrlAsync(
            jiraHttpClient,
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
            api2SearchUrl,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> TryGetJiraStatusBySearchUrlAsync(
        HttpClient jiraHttpClient,
        Uri searchUrl,
        CancellationToken cancellationToken)
    {
        using var response = await _httpRetryExecutor.GetAsync(
            jiraHttpClient,
            searchUrl,
            _jiraOptions.RetryCount,
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

    private async Task<JiraIssueInfo?> ReadJiraIssueInfoAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var issueDto = await _responseSerializer.SerializeAsync<JiraIssueStatusResponseDto>(
            response,
            cancellationToken).ConfigureAwait(false);

        return issueDto?.ToDomain(
            _jiraOptions.RequiredActionsFieldName,
            _jiraOptions.BreakingChangesFieldName);
    }

    private async Task<JiraSearchResult?> ReadJiraSearchResultAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var searchDto = await _responseSerializer.SerializeAsync<JiraSearchResponseDto>(
            response,
            cancellationToken).ConfigureAwait(false);

        return searchDto?.ToDomain();
    }

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJiraParser _jiraParser;
    private readonly IHttpRetryExecutor _httpRetryExecutor;
    private readonly IResponseSerializer _responseSerializer;
    private readonly JiraOptions _jiraOptions;
    private readonly SemaphoreSlim _jiraSemaphore;
    private readonly ConcurrentDictionary<string, JiraTaskInfo> _jiraTaskInfoCacheByTask =
        new(StringComparer.OrdinalIgnoreCase);
}
