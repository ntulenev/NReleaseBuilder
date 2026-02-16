namespace NReleaseBuilder.Models;

/// <summary>
/// Repository tag reference with optional commit hash.
/// </summary>
public readonly record struct RepositoryTagReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryTagReference"/> struct.
    /// </summary>
    /// <param name="name">Tag name.</param>
    /// <param name="commitHash">Commit hash associated with tag.</param>
    public RepositoryTagReference(string name, string? commitHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        CommitHash = string.IsNullOrWhiteSpace(commitHash) ? null : commitHash.Trim();
    }

    /// <summary>
    /// Tag name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Commit hash associated with tag.
    /// </summary>
    public string? CommitHash { get; }
}
