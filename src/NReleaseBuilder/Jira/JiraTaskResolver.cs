using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions.Jira;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Jira.Internal;
using NReleaseBuilder.Jira.Internal.Models;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;

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
    public async Task PrimeTaskInfoCacheAsync(
        IReadOnlyList<CommitInfo> commitInfos,
        IReadOnlyList<JiraProjectName> projectNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commitInfos);
        ArgumentNullException.ThrowIfNull(projectNames);

        if (commitInfos.Count == 0 || projectNames.Count == 0)
        {
            return;
        }

        var jiraTasksToLoad = new HashSet<JiraTaskReference>(JiraTaskReferenceComparer.Instance);

        foreach (var commitInfo in commitInfos)
        {
            ArgumentNullException.ThrowIfNull(commitInfo);

            var jiraTask = _jiraParser.ExtractJiraTask(commitInfo, projectNames);
            var splitTasks = _jiraParser.SplitJiraTasks(jiraTask);
            foreach (var task in splitTasks)
            {
                if (_jiraTaskInfoCacheByTask.ContainsKey(task)
                    || _jiraTaskInfoLoadTasksByTask.ContainsKey(task))
                {
                    continue;
                }

                _ = jiraTasksToLoad.Add(task);
            }
        }

        if (jiraTasksToLoad.Count == 0)
        {
            return;
        }

        var batchResults = await _jiraIntegrationCore.TryGetJiraTaskInfosAsync(
            [.. jiraTasksToLoad],
            cancellationToken).ConfigureAwait(false);

        foreach (var (jiraTask, jiraTaskInfo) in batchResults)
        {
            if (!jiraTaskInfo.IsTransientStatus())
            {
                _ = _jiraTaskInfoCacheByTask.TryAdd(jiraTask, jiraTaskInfo);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _jiraSemaphore.Dispose();

    private async Task<JiraTaskInfo> GetJiraTaskInfoAsync(
        JiraTaskReference task,
        CancellationToken cancellationToken)
    {
        if (_jiraTaskInfoCacheByTask.TryGetValue(task, out var cachedTaskInfo))
        {
            return cachedTaskInfo;
        }

        var loadTask = _jiraTaskInfoLoadTasksByTask.GetOrAdd(
            task,
            static (jiraTask, state) => state.Resolver.ResolveTaskInfoAsync(jiraTask, state.CancellationToken),
            (Resolver: this, CancellationToken: cancellationToken));

        try
        {
            return await loadTask.ConfigureAwait(false);
        }
        finally
        {
            if (loadTask.IsCompleted)
            {
                _ = _jiraTaskInfoLoadTasksByTask.TryRemove(task, out _);
            }
        }
    }

    private async Task<JiraTaskInfo> ResolveTaskInfoAsync(
        JiraTaskReference task,
        CancellationToken cancellationToken)
    {
        if (_jiraTaskInfoCacheByTask.TryGetValue(task, out var cachedTaskInfo))
        {
            return cachedTaskInfo;
        }

        await _jiraSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_jiraTaskInfoCacheByTask.TryGetValue(task, out cachedTaskInfo))
            {
                return cachedTaskInfo;
            }

            var jiraTaskInfo = await _jiraIntegrationCore.TryGetJiraTaskInfoAsync(
                task,
                cancellationToken).ConfigureAwait(false);

            if (!jiraTaskInfo.IsTransientStatus())
            {
                _ = _jiraTaskInfoCacheByTask.TryAdd(task, jiraTaskInfo);
            }

            return jiraTaskInfo;
        }
        finally
        {
            _ = _jiraSemaphore.Release();
        }
    }

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
            var jiraTaskInfo = await GetJiraTaskInfoAsync(task, cancellationToken).ConfigureAwait(false);

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
    private readonly ConcurrentDictionary<JiraTaskReference, Task<JiraTaskInfo>> _jiraTaskInfoLoadTasksByTask =
        new(JiraTaskReferenceComparer.Instance);
}
