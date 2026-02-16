namespace NReleaseBuilder.Models;

public sealed class JiraIssueStatusResponseDto
{
    public JiraIssueFieldsDto? Fields { get; set; }
}

public sealed class JiraIssueFieldsDto
{
    public JiraStatusDto? Status { get; set; }
}

public sealed class JiraStatusDto
{
    public string? Name { get; set; }
}

public sealed class JiraSearchResponseDto
{
    public IReadOnlyList<JiraSearchIssueDto> Issues { get; set; } = [];
}

public sealed class JiraSearchIssueDto
{
    public JiraIssueFieldsDto? Fields { get; set; }
}
