using NReleaseBuilder.Models;

using NuGet.Versioning;

namespace NReleaseBuilder.Abstractions.Bitbucket;

/// <summary>
/// Loads and enriches Bitbucket tag data per repository.
/// </summary>
public interface IBitbucketTagClient
{
    /// <summary>
    /// Retrieves tag lookup results for the provided repositories.
    /// </summary>
    /// <param name="repositories">Repository names to process.</param>
    /// <param name="minCurrentVersionsByRepository">Minimum current version per repository.</param>
    /// <param name="progress">Optional progress callbacks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lookup results keyed by repository name.</returns>
    Task<Dictionary<RepositoryName, RepositoryTagLookup>> FetchRepositoryTagLookupsAsync(
        IReadOnlyList<RepositoryName> repositories,
        IReadOnlyDictionary<RepositoryName, NuGetVersion> minCurrentVersionsByRepository,
        BitbucketProgressCallbacks? progress,
        CancellationToken cancellationToken);
}
