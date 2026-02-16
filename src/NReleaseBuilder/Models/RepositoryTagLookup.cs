namespace NReleaseBuilder.Models;

/// <summary>
/// Lookup result for repository tags.
/// </summary>
public readonly record struct RepositoryTagLookup
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryTagLookup"/> struct.
    /// </summary>
    /// <param name="isRepositoryMissing">Whether repository does not exist.</param>
    /// <param name="error">Error details for failed API calls.</param>
    /// <param name="tags">Resolved tags when lookup succeeds.</param>
    public RepositoryTagLookup(bool isRepositoryMissing, string? error, IReadOnlyList<RepositoryTagInfo> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var normalizedError = string.IsNullOrWhiteSpace(error) ? null : error.Trim();

        if (isRepositoryMissing && normalizedError is not null)
        {
            throw new ArgumentException(
                "Repository-not-found lookup cannot contain an error message.",
                nameof(error));
        }

        IsRepositoryMissing = isRepositoryMissing;
        Error = normalizedError;
        Tags = tags;
    }

    /// <summary>
    /// Whether repository does not exist.
    /// </summary>
    public bool IsRepositoryMissing { get; }

    /// <summary>
    /// Error details for failed API calls.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Resolved tags when lookup succeeds.
    /// </summary>
    public IReadOnlyList<RepositoryTagInfo> Tags { get; }

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
