namespace NReleaseBuilder.Transport.Models;

/// <summary>
/// Jira issue status DTO.
/// </summary>
public sealed class JiraStatusDto
{
    /// <summary>
    /// Status display name.
    /// </summary>
    public string? Name { get; init; }
}
