using NReleaseBuilder.Models;

namespace NReleaseBuilder.Transport;

/// <summary>
/// Mapping helpers from transport DTOs to domain models.
/// </summary>
public static class TransportMappings
{
    /// <summary>
    /// Converts a tag page DTO into a domain model.
    /// </summary>
    /// <param name="dto">Tag page DTO.</param>
    /// <returns>Domain tag page.</returns>
    public static RepositoryTagPage ToDomain(this TagPageDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var valueDtos = dto.Values ?? [];
        var values = new List<RepositoryTagReference>(valueDtos.Count);

        foreach (var valueDto in valueDtos)
        {
            if (valueDto is null || string.IsNullOrWhiteSpace(valueDto.Name))
            {
                continue;
            }

            var tagName = valueDto.Name.Trim();
            var commitHash = NormalizeOptional(valueDto.Target?.Hash);
            values.Add(new RepositoryTagReference(tagName, commitHash));
        }

        return new RepositoryTagPage(values, CreateUriOrNull(dto.Next));
    }

    /// <summary>
    /// Converts a commit DTO into a domain model.
    /// </summary>
    /// <param name="dto">Commit DTO.</param>
    /// <returns>Domain commit model.</returns>
    public static CommitInfo ToDomain(this CommitDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new CommitInfo(dto.Message);
    }

    /// <summary>
    /// Converts a Jira issue DTO into a domain model.
    /// </summary>
    /// <param name="dto">Jira issue DTO.</param>
    /// <returns>Domain Jira issue model.</returns>
    public static JiraIssueInfo ToDomain(this JiraIssueStatusResponseDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var statusNameText = dto.Fields?.Status?.Name;
        JiraStatusName? statusName = JiraStatusName.TryCreate(statusNameText, out var parsedStatusName)
            ? parsedStatusName
            : null;

        return new JiraIssueInfo(statusName);
    }

    /// <summary>
    /// Converts a Jira search DTO into a domain model.
    /// </summary>
    /// <param name="dto">Jira search DTO.</param>
    /// <returns>Domain Jira search model.</returns>
    public static JiraSearchResult ToDomain(this JiraSearchResponseDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var issueDtos = dto.Issues ?? [];
        var issues = new List<JiraIssueInfo>(issueDtos.Count);

        foreach (var issueDto in issueDtos)
        {
            if (issueDto is null)
            {
                continue;
            }

            var statusNameText = issueDto.Fields?.Status?.Name;
            JiraStatusName? statusName = JiraStatusName.TryCreate(statusNameText, out var parsedStatusName)
                ? parsedStatusName
                : null;

            issues.Add(new JiraIssueInfo(statusName));
        }

        return new JiraSearchResult(issues);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Uri? CreateUriOrNull(string? next)
    {
        if (string.IsNullOrWhiteSpace(next))
        {
            return null;
        }

        if (Uri.TryCreate(next, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        if (Uri.TryCreate(next, UriKind.Relative, out var relative))
        {
            return relative;
        }

        return null;
    }
}
