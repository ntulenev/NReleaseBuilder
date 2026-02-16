namespace NReleaseBuilder.Models;

/// <summary>
/// Jira issue response DTO.
/// </summary>
public sealed class JiraIssueStatusResponseDto
{
    /// <summary>
    /// Issue fields payload.
    /// </summary>
    public JiraIssueFieldsDto? Fields { get; set; }
}

/// <summary>
/// Jira issue fields DTO.
/// </summary>
public sealed class JiraIssueFieldsDto
{
    /// <summary>
    /// Issue status payload.
    /// </summary>
    public JiraStatusDto? Status { get; set; }
}

/// <summary>
/// Jira issue status DTO.
/// </summary>
public sealed class JiraStatusDto
{
    /// <summary>
    /// Status display name.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Jira search response DTO.
/// </summary>
public sealed class JiraSearchResponseDto
{
    /// <summary>
    /// Search result issues.
    /// </summary>
    public IReadOnlyList<JiraSearchIssueDto> Issues { get; set; } = [];
}

/// <summary>
/// Jira search issue DTO.
/// </summary>
public sealed class JiraSearchIssueDto
{
    /// <summary>
    /// Issue fields payload.
    /// </summary>
    public JiraIssueFieldsDto? Fields { get; set; }
}
