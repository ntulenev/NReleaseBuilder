using NReleaseBuilder.Models;

namespace NReleaseBuilder.Bitbucket.Internal.Models;

/// <summary>
/// Result of loading repository tag references from Bitbucket.
/// </summary>
public readonly record struct RepositoryTagReferenceLoadResult(
    bool IsRepositoryMissing,
    string? Error,
    IReadOnlyList<RepositoryTagReference> Tags)
{
    /// <summary>
    /// Creates a result indicating that repository was not found.
    /// </summary>
    /// <returns>Not-found result.</returns>
    public static RepositoryTagReferenceLoadResult RepoNotFound() => new(true, null, []);

    /// <summary>
    /// Creates a result indicating that API call failed.
    /// </summary>
    /// <param name="error">Error details.</param>
    /// <returns>API error result.</returns>
    public static RepositoryTagReferenceLoadResult ApiError(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new RepositoryTagReferenceLoadResult(false, error, []);
    }

    /// <summary>
    /// Creates a successful result with loaded tags.
    /// </summary>
    /// <param name="tags">Loaded tags.</param>
    /// <returns>Successful result.</returns>
    public static RepositoryTagReferenceLoadResult Success(IReadOnlyList<RepositoryTagReference> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        return new RepositoryTagReferenceLoadResult(false, null, tags);
    }
}
