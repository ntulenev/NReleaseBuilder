namespace NReleaseBuilder.Transport.Models;

/// <summary>
/// Jira search response DTO.
/// </summary>
public sealed class JiraSearchResponseDto
{
    /// <summary>
    /// Search result issues.
    /// </summary>
    public IReadOnlyList<JiraSearchIssueDto> Issues { get; init; } = [];
}
