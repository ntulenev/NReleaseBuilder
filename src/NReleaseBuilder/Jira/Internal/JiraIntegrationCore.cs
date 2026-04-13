using System.Net;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions.Jira;
using NReleaseBuilder.Abstractions.Transport;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Jira.Internal.Models;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Transport;
using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Jira.Internal;

/// <summary>
/// Jira integration service that loads Jira task metadata from Jira API.
/// </summary>
public sealed class JiraIntegrationCore : IJiraIntegrationCore, IDisposable
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
        var fieldIdentifiers = await ResolveFieldIdentifiersAsync(
            jiraHttpClient,
            cancellationToken).ConfigureAwait(false);
        var issueUrl = BuildIssueUrl(jiraTask, fieldIdentifiers);

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

        var issue = await ReadJiraIssueInfoAsync(
            response,
            fieldIdentifiers,
            cancellationToken).ConfigureAwait(false);
        return JiraTaskInfo.FromIssueInfo(issue);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<JiraTaskReference, JiraTaskInfo>> TryGetJiraTaskInfosAsync(
        IReadOnlyList<JiraTaskReference> jiraTasks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jiraTasks);

        if (jiraTasks.Count == 0)
        {
            return new Dictionary<JiraTaskReference, JiraTaskInfo>(JiraTaskReferenceComparer.Instance);
        }

        if (jiraTasks.Count == 1)
        {
            var jiraTask = jiraTasks[0];
            return new Dictionary<JiraTaskReference, JiraTaskInfo>(JiraTaskReferenceComparer.Instance)
            {
                [jiraTask] = await TryGetJiraTaskInfoAsync(jiraTask, cancellationToken).ConfigureAwait(false),
            };
        }

        var distinctTasks = jiraTasks
            .Distinct(JiraTaskReferenceComparer.Instance)
            .ToArray();
        var jiraHttpClient = _httpClientFactory.CreateClient(HttpClientNames.JIRA);
        var fieldIdentifiers = await ResolveFieldIdentifiersAsync(
            jiraHttpClient,
            cancellationToken).ConfigureAwait(false);

        if (fieldIdentifiers.UseExpandedNames)
        {
            return await LoadIndividuallyAsync(distinctTasks, cancellationToken).ConfigureAwait(false);
        }

        var results = new Dictionary<JiraTaskReference, JiraTaskInfo>(JiraTaskReferenceComparer.Instance);

        foreach (var jiraTaskChunk in distinctTasks.Chunk(JIRA_BATCH_SIZE))
        {
            var chunkResults = await TryGetJiraTaskInfosBySearchAsync(
                jiraHttpClient,
                jiraTaskChunk,
                fieldIdentifiers,
                cancellationToken).ConfigureAwait(false);

            if (chunkResults is null)
            {
                foreach (var jiraTask in jiraTaskChunk)
                {
                    results[jiraTask] = await TryGetJiraTaskInfoAsync(jiraTask, cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            foreach (var (jiraTask, jiraTaskInfo) in chunkResults)
            {
                results[jiraTask] = jiraTaskInfo;
            }
        }

        return results;
    }

    /// <inheritdoc />
    public void Dispose() => _fieldIdentifiersSemaphore.Dispose();

    private async Task<IReadOnlyDictionary<JiraTaskReference, JiraTaskInfo>> LoadIndividuallyAsync(
        IReadOnlyList<JiraTaskReference> jiraTasks,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<JiraTaskReference, JiraTaskInfo>(JiraTaskReferenceComparer.Instance);

        foreach (var jiraTask in jiraTasks)
        {
            results[jiraTask] = await TryGetJiraTaskInfoAsync(jiraTask, cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    private async Task<JiraFieldIdentifiers> ResolveFieldIdentifiersAsync(
        HttpClient jiraHttpClient,
        CancellationToken cancellationToken)
    {
        if (!_jiraOptions.CheckReleaseAlerts)
        {
            return JiraFieldIdentifiers.WithoutAlerts;
        }

        if (_resolvedFieldIdentifiers is { } cachedFieldIdentifiers)
        {
            return cachedFieldIdentifiers;
        }

        await _fieldIdentifiersSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_resolvedFieldIdentifiers is { } doubleCheckedFieldIdentifiers)
            {
                return doubleCheckedFieldIdentifiers;
            }

            var configuredFieldIdentifiers = JiraFieldIdentifiers.FromConfiguredOptions(_jiraOptions);
            if (configuredFieldIdentifiers.HasAllAlertFieldIdentifiers)
            {
                _resolvedFieldIdentifiers = configuredFieldIdentifiers;
                return configuredFieldIdentifiers;
            }

            var resolvedFieldIdentifiers = await TryResolveFieldIdentifiersFromMetadataAsync(
                jiraHttpClient,
                cancellationToken).ConfigureAwait(false);

            _resolvedFieldIdentifiers = resolvedFieldIdentifiers;
            return resolvedFieldIdentifiers;
        }
        finally
        {
            _ = _fieldIdentifiersSemaphore.Release();
        }
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

    private async Task<Dictionary<JiraTaskReference, JiraTaskInfo>?> TryGetJiraTaskInfosBySearchAsync(
        HttpClient jiraHttpClient,
        IReadOnlyList<JiraTaskReference> jiraTasks,
        JiraFieldIdentifiers fieldIdentifiers,
        CancellationToken cancellationToken)
    {
        var api3SearchUrl = BuildBatchSearchUrl(
            jiraTasks,
            fieldIdentifiers,
            apiVersion: 3);

        var api3Results = await TryGetJiraTaskInfosBySearchUrlAsync(
            jiraHttpClient,
            api3SearchUrl,
            jiraTasks,
            fieldIdentifiers,
            cancellationToken).ConfigureAwait(false);

        if (api3Results is not null)
        {
            return api3Results;
        }

        var api2SearchUrl = BuildBatchSearchUrl(
            jiraTasks,
            fieldIdentifiers,
            apiVersion: 2);

        return await TryGetJiraTaskInfosBySearchUrlAsync(
            jiraHttpClient,
            api2SearchUrl,
            jiraTasks,
            fieldIdentifiers,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<JiraTaskReference, JiraTaskInfo>?> TryGetJiraTaskInfosBySearchUrlAsync(
        HttpClient jiraHttpClient,
        Uri searchUrl,
        IReadOnlyList<JiraTaskReference> jiraTasks,
        JiraFieldIdentifiers fieldIdentifiers,
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
            return null;
        }

        var searchResult = await _responseSerializer.SerializeAsync<JiraSearchResponseDto>(
            response,
            cancellationToken).ConfigureAwait(false);

        if (searchResult is null)
        {
            return null;
        }

        var results = new Dictionary<JiraTaskReference, JiraTaskInfo>(JiraTaskReferenceComparer.Instance);

        foreach (var issue in searchResult.Issues)
        {
            if (issue is null || !JiraTaskReference.TryCreate(issue.Key, out var jiraTask))
            {
                continue;
            }

            var issueInfo = new JiraIssueStatusResponseDto
            {
                Fields = issue.Fields,
                Names = searchResult.Names,
            }.ToDomain(
                _jiraOptions.RequiredActionsFieldName,
                _jiraOptions.BreakingChangesFieldName,
                fieldIdentifiers.RequiredActionsFieldId,
                fieldIdentifiers.BreakingChangesFieldId);

            results[jiraTask] = JiraTaskInfo.FromIssueInfo(issueInfo);
        }

        foreach (var jiraTask in jiraTasks)
        {
            if (!results.ContainsKey(jiraTask))
            {
                results[jiraTask] = JiraTaskInfo.NotFound("Not found");
            }
        }

        return results;
    }

    private async Task<JiraIssueInfo?> ReadJiraIssueInfoAsync(
        HttpResponseMessage response,
        JiraFieldIdentifiers fieldIdentifiers,
        CancellationToken cancellationToken)
    {
        var issueDto = await _responseSerializer.SerializeAsync<JiraIssueStatusResponseDto>(
            response,
            cancellationToken).ConfigureAwait(false);

        return issueDto?.ToDomain(
            _jiraOptions.RequiredActionsFieldName,
            _jiraOptions.BreakingChangesFieldName,
            fieldIdentifiers.RequiredActionsFieldId,
            fieldIdentifiers.BreakingChangesFieldId);
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

    private async Task<JiraFieldIdentifiers> TryResolveFieldIdentifiersFromMetadataAsync(
        HttpClient jiraHttpClient,
        CancellationToken cancellationToken)
    {
        var api3FieldIdentifiers = await TryResolveFieldIdentifiersFromMetadataUrlAsync(
            jiraHttpClient,
            new Uri("rest/api/3/field", UriKind.Relative),
            cancellationToken).ConfigureAwait(false);

        if (api3FieldIdentifiers.HasAllAlertFieldIdentifiers)
        {
            return api3FieldIdentifiers;
        }

        var api2FieldIdentifiers = await TryResolveFieldIdentifiersFromMetadataUrlAsync(
            jiraHttpClient,
            new Uri("rest/api/2/field", UriKind.Relative),
            cancellationToken).ConfigureAwait(false);

        return api2FieldIdentifiers.HasAllAlertFieldIdentifiers
            ? api2FieldIdentifiers
            : JiraFieldIdentifiers.FallbackToExpandedNames;
    }

    private async Task<JiraFieldIdentifiers> TryResolveFieldIdentifiersFromMetadataUrlAsync(
        HttpClient jiraHttpClient,
        Uri metadataUrl,
        CancellationToken cancellationToken)
    {
        using var response = await _httpRetryExecutor.GetAsync(
            jiraHttpClient,
            metadataUrl,
            _jiraOptions.RetryCount,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return JiraFieldIdentifiers.WithoutResolvedAlertIdentifiers;
        }

        var fieldDefinitions = await _responseSerializer.SerializeAsync<IReadOnlyList<JiraFieldDefinitionDto>>(
            response,
            cancellationToken).ConfigureAwait(false);

        return JiraFieldIdentifiers.FromFieldMetadata(
            fieldDefinitions,
            _jiraOptions.RequiredActionsFieldName,
            _jiraOptions.BreakingChangesFieldName);
    }

    private static Uri BuildIssueUrl(
        JiraTaskReference jiraTask,
        JiraFieldIdentifiers fieldIdentifiers)
    {
        var query = fieldIdentifiers.UseExpandedNames
            ? "expand=names"
            : $"fields={Uri.EscapeDataString(fieldIdentifiers.BuildIssueFieldsQuery())}";

        return new Uri(
            $"rest/api/3/issue/{Uri.EscapeDataString(jiraTask.Value)}?{query}",
            UriKind.Relative);
    }

    private static Uri BuildBatchSearchUrl(
        IReadOnlyList<JiraTaskReference> jiraTasks,
        JiraFieldIdentifiers fieldIdentifiers,
        int apiVersion)
    {
        var escapedTaskKeys = jiraTasks
            .Select(static jiraTask => $"\"{jiraTask.Value}\"");
        var jql = $"key in ({string.Join(", ", escapedTaskKeys)}) order by key";
        var fields = Uri.EscapeDataString(fieldIdentifiers.BuildIssueFieldsQuery());

        var query = $"jql={Uri.EscapeDataString(jql)}&fields={fields}&maxResults={jiraTasks.Count}";

        return apiVersion == 3
            ? new Uri($"rest/api/3/search/jql?{query}", UriKind.Relative)
            : new Uri($"rest/api/2/search?{query}", UriKind.Relative);
    }

    private readonly record struct JiraFieldIdentifiers(
        string? RequiredActionsFieldId,
        string? BreakingChangesFieldId,
        bool UseExpandedNames)
    {
        public static JiraFieldIdentifiers WithoutAlerts { get; } = new(null, null, false);

        public static JiraFieldIdentifiers WithoutResolvedAlertIdentifiers { get; } = new(null, null, false);

        public static JiraFieldIdentifiers FallbackToExpandedNames { get; } = new(null, null, true);

        public bool HasAllAlertFieldIdentifiers
            => !string.IsNullOrWhiteSpace(RequiredActionsFieldId)
                && !string.IsNullOrWhiteSpace(BreakingChangesFieldId);

        public string BuildIssueFieldsQuery()
        {
            var fields = new List<string>
            {
                "summary",
                "status",
            };

            if (!string.IsNullOrWhiteSpace(RequiredActionsFieldId))
            {
                fields.Add(RequiredActionsFieldId);
            }

            if (!string.IsNullOrWhiteSpace(BreakingChangesFieldId))
            {
                fields.Add(BreakingChangesFieldId);
            }

            return string.Join(
                ',',
                fields.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        public static JiraFieldIdentifiers FromConfiguredOptions(JiraOptions jiraOptions)
        {
            ArgumentNullException.ThrowIfNull(jiraOptions);

            return new JiraFieldIdentifiers(
                jiraOptions.ResolveRequiredActionsFieldId(),
                jiraOptions.ResolveBreakingChangesFieldId(),
                UseExpandedNames: false);
        }

        public static JiraFieldIdentifiers FromFieldMetadata(
            IReadOnlyList<JiraFieldDefinitionDto>? fieldDefinitions,
            string requiredActionsFieldName,
            string breakingChangesFieldName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(requiredActionsFieldName);
            ArgumentException.ThrowIfNullOrWhiteSpace(breakingChangesFieldName);

            if (fieldDefinitions is null || fieldDefinitions.Count == 0)
            {
                return WithoutResolvedAlertIdentifiers;
            }

            string? requiredActionsFieldId = null;
            string? breakingChangesFieldId = null;

            foreach (var fieldDefinition in fieldDefinitions)
            {
                if (fieldDefinition is null
                    || string.IsNullOrWhiteSpace(fieldDefinition.Id)
                    || string.IsNullOrWhiteSpace(fieldDefinition.Name))
                {
                    continue;
                }

                if (requiredActionsFieldId is null
                    && string.Equals(
                        fieldDefinition.Name,
                        requiredActionsFieldName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    requiredActionsFieldId = fieldDefinition.Id.Trim();
                }

                if (breakingChangesFieldId is null
                    && string.Equals(
                        fieldDefinition.Name,
                        breakingChangesFieldName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    breakingChangesFieldId = fieldDefinition.Id.Trim();
                }

                if (requiredActionsFieldId is not null && breakingChangesFieldId is not null)
                {
                    break;
                }
            }

            return new JiraFieldIdentifiers(
                requiredActionsFieldId,
                breakingChangesFieldId,
                UseExpandedNames: false);
        }
    }

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpRetryExecutor _httpRetryExecutor;
    private readonly IResponseSerializer _responseSerializer;
    private readonly JiraOptions _jiraOptions;
    private readonly SemaphoreSlim _fieldIdentifiersSemaphore = new(1, 1);
    private JiraFieldIdentifiers? _resolvedFieldIdentifiers;
    private const int JIRA_BATCH_SIZE = 20;
}
