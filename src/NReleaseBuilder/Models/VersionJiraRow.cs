namespace NReleaseBuilder.Models;

/// <summary>
/// Newer version row enriched with Jira task, title, and status.
/// </summary>
public readonly record struct VersionJiraRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersionJiraRow"/> struct.
    /// </summary>
    /// <param name="version">Version string.</param>
    /// <param name="jiraTask">Jira task key(s).</param>
    /// <param name="jiraTitle">Jira title(s).</param>
    /// <param name="jiraStatus">Jira status value(s).</param>
    /// <param name="taskAlertDetails">Per-task release alert details.</param>
    /// <param name="hasRequiredActions">Whether related Jira issues have Required Actions.</param>
    /// <param name="hasBreakingChanges">Whether related Jira issues have Breaking changes.</param>
    /// <param name="hasDependencyIssues">
    /// Whether Required Actions or Breaking changes contain links to other Jira tasks from configured projects.
    /// </param>
    public VersionJiraRow(
        VersionLabel version,
        JiraTaskReference jiraTask,
        JiraTitleReference jiraTitle,
        JiraStatusReference jiraStatus,
        IReadOnlyList<JiraTaskAlertDetails> taskAlertDetails,
        bool hasRequiredActions,
        bool hasBreakingChanges,
        bool hasDependencyIssues)
    {
        ArgumentNullException.ThrowIfNull(taskAlertDetails);

        Version = version;
        JiraTask = jiraTask;
        JiraTitle = jiraTitle;
        JiraStatus = jiraStatus;
        TaskAlertDetails = taskAlertDetails;
        HasRequiredActions = hasRequiredActions;
        HasBreakingChanges = hasBreakingChanges;
        HasDependencyIssues = hasDependencyIssues;
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
    /// Jira title(s).
    /// </summary>
    public JiraTitleReference JiraTitle { get; }

    /// <summary>
    /// Jira status value(s).
    /// </summary>
    public JiraStatusReference JiraStatus { get; }

    /// <summary>
    /// Per-task release alert details.
    /// </summary>
    public IReadOnlyList<JiraTaskAlertDetails> TaskAlertDetails { get; }

    /// <summary>
    /// Whether related Jira issues have Required Actions.
    /// </summary>
    public bool HasRequiredActions { get; }

    /// <summary>
    /// Whether related Jira issues have Breaking changes.
    /// </summary>
    public bool HasBreakingChanges { get; }

    /// <summary>
    /// Whether Required Actions or Breaking changes contain links to other Jira tasks from configured projects.
    /// </summary>
    public bool HasDependencyIssues { get; }
}
