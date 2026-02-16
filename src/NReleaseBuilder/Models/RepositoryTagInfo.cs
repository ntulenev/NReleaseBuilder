namespace NReleaseBuilder.Models;

/// <summary>
/// Repository tag enriched with Jira task and status information.
/// </summary>
/// <param name="Name">Tag name.</param>
/// <param name="JiraTask">Resolved Jira task key(s).</param>
/// <param name="JiraStatus">Resolved Jira status(es).</param>
public readonly record struct RepositoryTagInfo(
    VersionLabel Name,
    JiraTaskReference JiraTask,
    JiraStatusReference JiraStatus);
