using System.Text.Json;

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
    /// <param name="requiredActionsFieldName">Custom field display name for Required Actions.</param>
    /// <param name="breakingChangesFieldName">Custom field display name for Breaking changes.</param>
    /// <returns>Domain Jira issue model.</returns>
    public static JiraIssueInfo ToDomain(
        this JiraIssueStatusResponseDto dto,
        string requiredActionsFieldName,
        string breakingChangesFieldName)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredActionsFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(breakingChangesFieldName);

        var statusNameText = dto.Fields?.Status?.Name;
        JiraStatusName? statusName = JiraStatusName.TryCreate(statusNameText, out var parsedStatusName)
            ? parsedStatusName
            : null;
        var title = NormalizeIssueTitle(dto.Fields?.Summary);

        var requiredActionsDetails = ExtractCustomFieldText(dto, requiredActionsFieldName);
        var breakingChangesDetails = ExtractCustomFieldText(dto, breakingChangesFieldName);

        return new JiraIssueInfo(statusName, title, requiredActionsDetails, breakingChangesDetails);
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
            var title = NormalizeIssueTitle(issueDto.Fields?.Summary);

            issues.Add(new JiraIssueInfo(statusName, title, null, null));
        }

        return new JiraSearchResult(issues);
    }

    private static string? ExtractCustomFieldText(JiraIssueStatusResponseDto dto, string fieldDisplayName)
    {
        var fieldIdentifier = ResolveFieldIdentifierByDisplayName(dto.Names, fieldDisplayName);
        if (string.IsNullOrWhiteSpace(fieldIdentifier))
        {
            return null;
        }

        var additionalFields = dto.Fields?.AdditionalFields;
        if (additionalFields is null || !additionalFields.TryGetValue(fieldIdentifier, out var fieldValue))
        {
            return null;
        }

        return ExtractJsonFieldText(fieldValue);
    }

    private static string? ResolveFieldIdentifierByDisplayName(
        IReadOnlyDictionary<string, string?>? fieldNamesByIdentifier,
        string fieldDisplayName)
    {
        if (fieldNamesByIdentifier is null || fieldNamesByIdentifier.Count == 0)
        {
            return null;
        }

        foreach (var (fieldIdentifier, displayName) in fieldNamesByIdentifier)
        {
            if (string.Equals(displayName, fieldDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return fieldIdentifier;
            }
        }

        return null;
    }

    private static string? ExtractJsonFieldText(JsonElement fieldValue)
    {
        var lines = new List<string>();
        AppendJsonFieldText(fieldValue, lines);
        if (lines.Count == 0)
        {
            return null;
        }

        var text = string.Join(
            Environment.NewLine,
            lines.Where(static line => !string.IsNullOrWhiteSpace(line)));

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static void AppendJsonFieldText(JsonElement fieldValue, List<string> lines)
    {
        switch (fieldValue.ValueKind)
        {
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
                return;
            case JsonValueKind.Object:
                AppendObjectFieldText(fieldValue, lines);
                return;
            case JsonValueKind.Array:
                AppendArrayFieldText(fieldValue, lines);
                return;
            case JsonValueKind.String:
                AppendLine(lines, fieldValue.GetString());
                return;
            case JsonValueKind.Number:
                AppendLine(lines, fieldValue.GetRawText());
                return;
            case JsonValueKind.True:
                AppendLine(lines, "true");
                return;
            case JsonValueKind.False:
                AppendLine(lines, "false");
                return;
            default:
                return;
        }
    }

    private static void AppendObjectFieldText(JsonElement fieldValue, List<string> lines)
    {
        var linesBefore = lines.Count;

        if (fieldValue.TryGetProperty("text", out var textValue))
        {
            AppendJsonFieldText(textValue, lines);
        }

        if (fieldValue.TryGetProperty("value", out var valueValue))
        {
            AppendJsonFieldText(valueValue, lines);
        }

        if (fieldValue.TryGetProperty("content", out var contentValue))
        {
            AppendJsonFieldText(contentValue, lines);
        }

        if (lines.Count > linesBefore)
        {
            return;
        }

        foreach (var property in fieldValue.EnumerateObject())
        {
            AppendJsonFieldText(property.Value, lines);
        }

        if (lines.Count > linesBefore || !HasMeaningfulJsonValue(fieldValue))
        {
            return;
        }

        AppendLine(lines, fieldValue.GetRawText());
    }

    private static void AppendArrayFieldText(JsonElement fieldValue, List<string> lines)
    {
        foreach (var item in fieldValue.EnumerateArray())
        {
            AppendJsonFieldText(item, lines);
        }
    }

    private static void AppendLine(List<string> lines, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add(value.Trim());
        }
    }

    private static bool HasMeaningfulJsonValue(JsonElement fieldValue)
    {
        return fieldValue.ValueKind switch
        {
            JsonValueKind.Undefined => false,
            JsonValueKind.Null => false,
            JsonValueKind.Object => fieldValue.EnumerateObject().Any(static property => HasMeaningfulJsonValue(property.Value)),
            JsonValueKind.Array => fieldValue.EnumerateArray().Any(HasMeaningfulJsonValue),
            JsonValueKind.String => !string.IsNullOrWhiteSpace(fieldValue.GetString()),
            JsonValueKind.Number => true,
            JsonValueKind.True => true,
            JsonValueKind.False => true,
            _ => false,
        };
    }


    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeIssueTitle(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "N/A" : value.Trim();
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
