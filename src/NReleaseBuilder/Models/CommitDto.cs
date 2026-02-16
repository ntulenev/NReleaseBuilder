namespace NReleaseBuilder.Models;

/// <summary>
/// Bitbucket commit DTO.
/// </summary>
public sealed class CommitDto
{
    /// <summary>
    /// Commit message.
    /// </summary>
    public string? Message { get; set; }
}
