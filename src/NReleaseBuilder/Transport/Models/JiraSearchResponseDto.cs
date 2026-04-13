namespace NReleaseBuilder.Transport.Models;

/// <summary>
/// Jira search response DTO.
/// </summary>
public sealed class JiraSearchResponseDto
{
    /// <summary>
    /// Mapping of field identifiers to display names when <c>expand=names</c> is requested.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Names { get; init; }

    /// <summary>
    /// Search result issues.
    /// </summary>
    public IReadOnlyList<JiraSearchIssueDto> Issues { get; init; } = [];
}
