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
    public VersionJiraRow(
        VersionLabel version,
        JiraTaskReference jiraTask,
        JiraStatusReference jiraStatus)
    {
        Version = version;
        JiraTask = jiraTask;
        JiraStatus = jiraStatus;
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
}
