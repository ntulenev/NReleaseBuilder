namespace NReleaseBuilder.Transport.Models;

/// <summary>
/// Jira search issue DTO.
/// </summary>
public sealed class JiraSearchIssueDto
{
    /// <summary>
    /// Issue fields payload.
    /// </summary>
    public JiraIssueFieldsDto? Fields { get; init; }
}
