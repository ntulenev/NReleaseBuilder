using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Jira.Internal;
using NReleaseBuilder.Jira.Internal.Models;
using NReleaseBuilder.Models;

namespace NReleaseBuilder.Jira;

/// <summary>
/// Jira task resolver that extracts task keys from commit messages and loads Jira metadata.
/// </summary>
public sealed class JiraTaskResolver : IJiraTaskResolver, IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraTaskResolver"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    /// <param name="jiraParser">Jira parser abstraction.</param>
    /// <param name="jiraIntegrationCore">Jira integration abstraction.</param>
    public JiraTaskResolver(
        IOptions<AppSettings> options,
        IJiraParser jiraParser,
        IJiraIntegrationCore jiraIntegrationCore)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(jiraParser);
        ArgumentNullException.ThrowIfNull(jiraIntegrationCore);

        var appSettings = options.Value;

        _jiraParser = jiraParser;
        _jiraIntegrationCore = jiraIntegrationCore;
        _jiraOptions = appSettings.Jira;
        _jiraSemaphore = new SemaphoreSlim(
            _jiraOptions.MaxParallelRequests,
            _jiraOptions.MaxParallelRequests);
    }

    /// <inheritdoc />
    public async Task<JiraTaskResolution> ResolveFromCommitMessageAsync(
        CommitInfo commitInfo,
        IReadOnlyList<JiraProjectName> projectNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commitInfo);
        ArgumentNullException.ThrowIfNull(projectNames);

        var jiraTask = _jiraParser.ExtractJiraTask(commitInfo, projectNames);
        return await ResolveJiraTaskResolutionAsync(
            jiraTask,
            projectNames,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => _jiraSemaphore.Dispose();

    private async Task<JiraTaskResolution> ResolveJiraTaskResolutionAsync(
        JiraTaskReference jiraTask,
        IReadOnlyList<JiraProjectName> projectNames,
        CancellationToken cancellationToken)
    {
        var jiraTasks = _jiraParser.SplitJiraTasks(jiraTask);
        if (jiraTasks.Length == 0)
        {
            return JiraTaskResolution.NotAvailable(jiraTask);
        }

        var jiraStatuses = new List<JiraStatusReference>(jiraTasks.Length);
        var jiraTitles = new List<JiraTitleReference>(jiraTasks.Length);
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
                    jiraTaskInfo = await _jiraIntegrationCore.TryGetJiraTaskInfoAsync(
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

            var jiraStatus = new JiraStatusReference(jiraTaskInfo.Status);
            var jiraTitle = new JiraTitleReference(jiraTaskInfo.Title);
            jiraStatuses.Add(jiraStatus);
            jiraTitles.Add(jiraTitle);
            var requiredActionsDetails = _jiraOptions.CheckReleaseAlerts
                ? jiraTaskInfo.RequiredActionsDetails
                : null;
            var breakingChangesDetails = _jiraOptions.CheckReleaseAlerts
                ? jiraTaskInfo.BreakingChangesDetails
                : null;

            taskAlertDetails.Add(new JiraTaskAlertDetails(
                task,
                jiraTitle,
                jiraStatus,
                requiredActionsDetails,
                breakingChangesDetails));

            if (_jiraOptions.CheckReleaseAlerts)
            {
                hasRequiredActions |= jiraTaskInfo.HasRequiredActions;
                hasBreakingChanges |= jiraTaskInfo.HasBreakingChanges;
                hasDependencyIssues |= _jiraParser.HasDependencyIssue(
                    task,
                    new JiraAlertDetails(
                        jiraTaskInfo.RequiredActionsDetails,
                        jiraTaskInfo.BreakingChangesDetails),
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

    private readonly IJiraParser _jiraParser;
    private readonly IJiraIntegrationCore _jiraIntegrationCore;
    private readonly JiraOptions _jiraOptions;
    private readonly SemaphoreSlim _jiraSemaphore;
    private readonly ConcurrentDictionary<JiraTaskReference, JiraTaskInfo> _jiraTaskInfoCacheByTask =
        new(JiraTaskReferenceComparer.Instance);
}
