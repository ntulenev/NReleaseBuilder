namespace NReleaseBuilder.Transport.Models;

/// <summary>
/// Jira search issue DTO.
/// </summary>
public sealed class JiraSearchIssueDto
{
    /// <summary>
    /// Issue key.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Issue fields payload.
    /// </summary>
    public JiraIssueFieldsDto? Fields { get; init; }
}
