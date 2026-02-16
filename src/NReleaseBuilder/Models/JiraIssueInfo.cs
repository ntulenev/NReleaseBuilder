namespace NReleaseBuilder.Models;

/// <summary>
/// Jira issue domain model with resolved status.
/// </summary>
public sealed class JiraIssueInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraIssueInfo"/> class.
    /// </summary>
    /// <param name="statusName">Resolved status name.</param>
    /// <param name="hasRequiredActions">Whether issue has non-empty Required Actions field.</param>
    /// <param name="hasBreakingChanges">Whether issue has non-empty Breaking changes field.</param>
    public JiraIssueInfo(
        JiraStatusName? statusName,
        bool hasRequiredActions,
        bool hasBreakingChanges)
    {
        StatusName = statusName;
        HasRequiredActions = hasRequiredActions;
        HasBreakingChanges = hasBreakingChanges;
    }

    /// <summary>
    /// Resolved issue status name.
    /// </summary>
    public JiraStatusName? StatusName { get; }

    /// <summary>
    /// Whether issue has non-empty Required Actions field.
    /// </summary>
    public bool HasRequiredActions { get; }

    /// <summary>
    /// Whether issue has non-empty Breaking changes field.
    /// </summary>
    public bool HasBreakingChanges { get; }
}
