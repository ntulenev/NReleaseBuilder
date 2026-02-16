namespace NReleaseBuilder.Models;

/// <summary>
/// Repository tag enriched with Jira task and status information.
/// </summary>
public readonly record struct RepositoryTagInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryTagInfo"/> struct.
    /// </summary>
    /// <param name="name">Tag name.</param>
    /// <param name="jiraTask">Resolved Jira task key(s).</param>
    /// <param name="jiraStatus">Resolved Jira status(es).</param>
    /// <param name="hasRequiredActions">Whether related Jira issues have Required Actions.</param>
    /// <param name="hasBreakingChanges">Whether related Jira issues have Breaking changes.</param>
    public RepositoryTagInfo(
        VersionLabel name,
        JiraTaskReference jiraTask,
        JiraStatusReference jiraStatus,
        bool hasRequiredActions,
        bool hasBreakingChanges)
    {
        Name = name;
        JiraTask = jiraTask;
        JiraStatus = jiraStatus;
        HasRequiredActions = hasRequiredActions;
        HasBreakingChanges = hasBreakingChanges;
    }

    /// <summary>
    /// Tag name.
    /// </summary>
    public VersionLabel Name { get; }

    /// <summary>
    /// Resolved Jira task key(s).
    /// </summary>
    public JiraTaskReference JiraTask { get; }

    /// <summary>
    /// Resolved Jira status(es).
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
