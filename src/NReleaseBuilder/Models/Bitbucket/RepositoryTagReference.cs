namespace NReleaseBuilder.Models.Bitbucket;

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
    public RepositoryTagReference(VersionLabel name, CommitHash? commitHash)
    {
        Name = name;
        CommitHash = commitHash;
    }

    /// <summary>
    /// Tag name.
    /// </summary>
    public VersionLabel Name { get; }

    /// <summary>
    /// Commit hash associated with tag.
    /// </summary>
    public CommitHash? CommitHash { get; }
}
