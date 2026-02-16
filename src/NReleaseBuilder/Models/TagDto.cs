namespace NReleaseBuilder.Models;

/// <summary>
/// Bitbucket tag DTO.
/// </summary>
public sealed class TagDto
{
    /// <summary>
    /// Tag name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Tag target payload.
    /// </summary>
    public TagTargetDto? Target { get; set; }
}
