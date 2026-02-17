using System.Net;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Jira.Internal.Models;
using NReleaseBuilder.Models;
using NReleaseBuilder.Transport;
using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Jira.Internal;

/// <summary>
/// Jira integration service that loads Jira task metadata from Jira API.
/// </summary>
public sealed class JiraIntegrationCore : IJiraIntegrationCore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraIntegrationCore"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="options">Application settings options.</param>
    /// <param name="httpRetryExecutor">Shared HTTP retry executor.</param>
    /// <param name="responseSerializer">HTTP response serializer.</param>
    public JiraIntegrationCore(
        IHttpClientFactory httpClientFactory,
        IOptions<AppSettings> options,
        IHttpRetryExecutor httpRetryExecutor,
        IResponseSerializer responseSerializer)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpRetryExecutor);
        ArgumentNullException.ThrowIfNull(responseSerializer);

        _httpClientFactory = httpClientFactory;
        _httpRetryExecutor = httpRetryExecutor;
        _responseSerializer = responseSerializer;
        _jiraOptions = options.Value.Jira;
    }

    /// <inheritdoc />
    public async Task<JiraTaskInfo> TryGetJiraTaskInfoAsync(
        JiraTaskReference jiraTask,
        CancellationToken cancellationToken)
    {
        var jiraHttpClient = _httpClientFactory.CreateClient(HttpClientNames.JIRA);
        var issueUrl = new Uri(
            $"rest/api/3/issue/{Uri.EscapeDataString(jiraTask.Value)}?expand=names",
            UriKind.Relative);

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
        return JiraTaskInfo.FromIssueInfo(issue);
    }

    private async Task<string?> TryGetJiraStatusBySearchAsync(
        HttpClient jiraHttpClient,
        JiraTaskReference jiraTask,
        CancellationToken cancellationToken)
    {
        var jql = $"key = \"{jiraTask.Value}\"";

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
    private readonly IHttpRetryExecutor _httpRetryExecutor;
    private readonly IResponseSerializer _responseSerializer;
    private readonly JiraOptions _jiraOptions;
}
