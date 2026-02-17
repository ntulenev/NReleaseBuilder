namespace NReleaseBuilder.Transport.Models;

/// <summary>
/// Jira issue response DTO.
/// </summary>
public sealed class JiraIssueStatusResponseDto
{
    /// <summary>
    /// Issue fields payload.
    /// </summary>
    public JiraIssueFieldsDto? Fields { get; init; }

    /// <summary>
    /// Mapping of field identifiers to display names when <c>expand=names</c> is requested.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Names { get; init; }
}
