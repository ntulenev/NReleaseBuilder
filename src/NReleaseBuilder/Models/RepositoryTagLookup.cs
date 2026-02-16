namespace NReleaseBuilder.Models;

/// <summary>
/// Lookup result for repository tags.
/// </summary>
public readonly record struct RepositoryTagLookup
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryTagLookup"/> struct.
    /// </summary>
    /// <param name="resolvedRepository">Repository name used for Bitbucket lookup.</param>
    /// <param name="isRepositoryMissing">Whether repository does not exist.</param>
    /// <param name="error">Error details for failed API calls.</param>
    /// <param name="tags">Resolved tags when lookup succeeds.</param>
    public RepositoryTagLookup(
        RepositoryName resolvedRepository,
        bool isRepositoryMissing,
        string? error,
        IReadOnlyList<RepositoryTagInfo> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        if (string.IsNullOrWhiteSpace(resolvedRepository.Value))
        {
            throw new ArgumentException(
                "Resolved repository must not be empty.",
                nameof(resolvedRepository));
        }

        var normalizedError = string.IsNullOrWhiteSpace(error) ? null : error.Trim();

        if (isRepositoryMissing && normalizedError is not null)
        {
            throw new ArgumentException(
                "Repository-not-found lookup cannot contain an error message.",
                nameof(error));
        }

        ResolvedRepository = resolvedRepository;
        IsRepositoryMissing = isRepositoryMissing;
        Error = normalizedError;
        Tags = tags;
    }

    /// <summary>
    /// Repository name used for Bitbucket lookup.
    /// </summary>
    public RepositoryName ResolvedRepository { get; }

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
    /// <param name="resolvedRepository">Repository name used for Bitbucket lookup.</param>
    /// <returns>Repository-not-found lookup result.</returns>
    public static RepositoryTagLookup RepoNotFound(RepositoryName resolvedRepository) =>
        new(resolvedRepository, true, null, []);

    /// <summary>
    /// Creates lookup result for API error.
    /// </summary>
    /// <param name="resolvedRepository">Repository name used for Bitbucket lookup.</param>
    /// <param name="error">Error details.</param>
    /// <returns>API-error lookup result.</returns>
    public static RepositoryTagLookup ApiError(RepositoryName resolvedRepository, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new RepositoryTagLookup(resolvedRepository, false, error, []);
    }

    /// <summary>
    /// Creates successful lookup result.
    /// </summary>
    /// <param name="resolvedRepository">Repository name used for Bitbucket lookup.</param>
    /// <param name="tags">Resolved repository tags.</param>
    /// <returns>Successful lookup result.</returns>
    public static RepositoryTagLookup Success(
        RepositoryName resolvedRepository,
        IReadOnlyList<RepositoryTagInfo> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return new RepositoryTagLookup(resolvedRepository, false, null, tags);
    }
}
