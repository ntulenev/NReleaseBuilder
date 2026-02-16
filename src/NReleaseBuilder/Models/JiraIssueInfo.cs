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
    public JiraIssueInfo(JiraStatusName? statusName)
    {
        StatusName = statusName;
    }

    /// <summary>
    /// Resolved issue status name.
    /// </summary>
    public JiraStatusName? StatusName { get; }
}
