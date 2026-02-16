namespace NReleaseBuilder.Models;

/// <summary>
/// Newer version row enriched with Jira task and status.
/// </summary>
public readonly record struct VersionJiraRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersionJiraRow"/> struct.
    /// </summary>
    /// <param name="version">Version string.</param>
    /// <param name="jiraTask">Jira task key(s).</param>
    /// <param name="jiraStatus">Jira status value(s).</param>
    /// <param name="hasRequiredActions">Whether related Jira issues have Required Actions.</param>
    /// <param name="hasBreakingChanges">Whether related Jira issues have Breaking changes.</param>
    public VersionJiraRow(
        VersionLabel version,
        JiraTaskReference jiraTask,
        JiraStatusReference jiraStatus,
        bool hasRequiredActions,
        bool hasBreakingChanges)
    {
        Version = version;
        JiraTask = jiraTask;
        JiraStatus = jiraStatus;
        HasRequiredActions = hasRequiredActions;
        HasBreakingChanges = hasBreakingChanges;
    }

    /// <summary>
    /// Version string.
    /// </summary>
    public VersionLabel Version { get; }

    /// <summary>
    /// Jira task key(s).
    /// </summary>
    public JiraTaskReference JiraTask { get; }

    /// <summary>
    /// Jira status value(s).
    /// </summary>
    public JiraStatusReference JiraStatus { get; }

    /// <summary>
    /// Whether related Jira issues have Required Actions.
    /// </summary>
    public bool HasRequiredActions { get; }

    /// <summary>
    /// Whether related Jira issues have Breaking changes.
    /// </summary>
    public bool HasBreakingChanges { get; }
}
