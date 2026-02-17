using System.Text.Json;
using System.Text.Json.Serialization;

namespace NReleaseBuilder.Transport.Models;

/// <summary>
/// Jira issue fields DTO.
/// </summary>
public sealed class JiraIssueFieldsDto
{
    /// <summary>
    /// Issue status payload.
    /// </summary>
    public JiraStatusDto? Status { get; init; }

    /// <summary>
    /// Issue title/summary.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Additional Jira fields including custom fields.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalFields { get; init; }
}
