namespace NReleaseBuilder.Transport.Models;

/// <summary>
/// Jira field metadata DTO.
/// </summary>
public sealed class JiraFieldDefinitionDto
{
    /// <summary>
    /// Field identifier.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Field display name.
    /// </summary>
    public string? Name { get; init; }
}
