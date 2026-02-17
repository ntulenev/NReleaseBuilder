using NReleaseBuilder.Models;

using NuGet.Versioning;

namespace NReleaseBuilder.Abstractions.Bitbucket;

/// <summary>
/// Loads repository tag lookups from Bitbucket in batches.
/// </summary>
public interface IRepositoryTagLookupBatchLoader
{
    /// <summary>
    /// Loads repository tag lookups for the provided repositories.
    /// </summary>
    /// <param name="repositories">Repositories to load.</param>
    /// <param name="minCurrentVersionsByRepository">Minimum current version per repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tag lookups keyed by repository name; <see langword="null"/> when loading fails.</returns>
    Task<Dictionary<RepositoryName, RepositoryTagLookup>?> LoadAsync(
        IReadOnlyList<RepositoryName> repositories,
        IReadOnlyDictionary<RepositoryName, NuGetVersion> minCurrentVersionsByRepository,
        CancellationToken cancellationToken);
}
