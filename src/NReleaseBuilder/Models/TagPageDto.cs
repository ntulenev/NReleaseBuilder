namespace NReleaseBuilder.Models;

/// <summary>
/// Bitbucket tag page DTO.
/// </summary>
public sealed class TagPageDto
{
    /// <summary>
    /// Page tag items.
    /// </summary>
    public IReadOnlyList<TagDto>? Values { get; init; }

    /// <summary>
    /// Next page URL.
    /// </summary>
    public string? Next { get; set; }
}
