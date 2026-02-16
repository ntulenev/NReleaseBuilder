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
    public RepositoryTagInfo(
        VersionLabel name,
        JiraTaskReference jiraTask,
        JiraStatusReference jiraStatus)
    {
        Name = name;
        JiraTask = jiraTask;
        JiraStatus = jiraStatus;
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
}
