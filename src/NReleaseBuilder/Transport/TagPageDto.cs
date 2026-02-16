namespace NReleaseBuilder.Transport;

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
    public string? Next { get; init; }
}

/// <summary>
/// Bitbucket tag DTO.
/// </summary>
public sealed class TagDto
{
    /// <summary>
    /// Tag name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Tag target payload.
    /// </summary>
    public TagTargetDto? Target { get; init; }
}

/// <summary>
/// Bitbucket tag target DTO.
/// </summary>
public sealed class TagTargetDto
{
    /// <summary>
    /// Commit hash.
    /// </summary>
    public string? Hash { get; init; }
}
