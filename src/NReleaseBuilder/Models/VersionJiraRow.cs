namespace NReleaseBuilder.Models;

/// <summary>
/// Newer version row enriched with Jira task and status.
/// </summary>
/// <param name="Version">Version string.</param>
/// <param name="JiraTask">Jira task key(s).</param>
/// <param name="JiraStatus">Jira status value(s).</param>
public readonly record struct VersionJiraRow(string Version, string JiraTask, string JiraStatus);
