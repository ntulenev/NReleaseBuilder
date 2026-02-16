namespace NReleaseBuilder.Models;

/// <summary>
/// Lookup result for repository tags.
/// </summary>
/// <param name="IsRepositoryMissing">Whether repository does not exist.</param>
/// <param name="Error">Error details for failed API calls.</param>
/// <param name="Tags">Resolved tags when lookup succeeds.</param>
public readonly record struct RepositoryTagLookup(bool IsRepositoryMissing, string? Error, IReadOnlyList<RepositoryTagInfo> Tags)
{
    /// <summary>
    /// Creates lookup result for missing repository.
    /// </summary>
    /// <returns>Repository-not-found lookup result.</returns>
    public static RepositoryTagLookup RepoNotFound() => new(true, null, []);

    /// <summary>
    /// Creates lookup result for API error.
    /// </summary>
    /// <param name="error">Error details.</param>
    /// <returns>API-error lookup result.</returns>
    public static RepositoryTagLookup ApiError(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new RepositoryTagLookup(false, error, []);
    }

    /// <summary>
    /// Creates successful lookup result.
    /// </summary>
    /// <param name="tags">Resolved repository tags.</param>
    /// <returns>Successful lookup result.</returns>
    public static RepositoryTagLookup Success(IReadOnlyList<RepositoryTagInfo> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return new RepositoryTagLookup(false, null, tags);
    }
}
