namespace NReleaseBuilder.Models;

/// <summary>
/// Bitbucket tag target DTO.
/// </summary>
public sealed class TagTargetDto
{
    /// <summary>
    /// Commit hash.
    /// </summary>
    public string? Hash { get; set; }
}
