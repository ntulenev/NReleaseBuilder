
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Models.Bitbucket;

/// <summary>
/// Repository tag enriched with Jira task, title, and status information.
/// </summary>
public readonly record struct RepositoryTagInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryTagInfo"/> struct.
    /// </summary>
    /// <param name="name">Tag name.</param>
    /// <param name="jiraTask">Resolved Jira task key(s).</param>
    /// <param name="jiraTitle">Resolved Jira title(s).</param>
    /// <param name="jiraStatus">Resolved Jira status(es).</param>
    /// <param name="taskAlertDetails">Per-task release alert details.</param>
    /// <param name="hasRequiredActions">Whether related Jira issues have Required Actions.</param>
    /// <param name="hasBreakingChanges">Whether related Jira issues have Breaking changes.</param>
    /// <param name="hasDependencyIssues">
    /// Whether Required Actions or Breaking changes contain links to other Jira tasks from configured projects.
    /// </param>
    public RepositoryTagInfo(
        VersionLabel name,
        JiraTaskReference jiraTask,
        JiraTitleReference jiraTitle,
        JiraStatusReference jiraStatus,
        IReadOnlyList<JiraTaskAlertDetails> taskAlertDetails,
        bool hasRequiredActions,
        bool hasBreakingChanges,
        bool hasDependencyIssues)
    {
        ArgumentNullException.ThrowIfNull(taskAlertDetails);

        Name = name;
        JiraTask = jiraTask;
        JiraTitle = jiraTitle;
        JiraStatus = jiraStatus;
        TaskAlertDetails = taskAlertDetails;
        HasRequiredActions = hasRequiredActions;
        HasBreakingChanges = hasBreakingChanges;
        HasDependencyIssues = hasDependencyIssues;
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
    /// Resolved Jira title(s).
    /// </summary>
    public JiraTitleReference JiraTitle { get; }

    /// <summary>
    /// Resolved Jira status(es).
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
