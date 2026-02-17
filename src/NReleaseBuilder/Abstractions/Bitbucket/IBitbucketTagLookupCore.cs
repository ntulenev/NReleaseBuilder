using NReleaseBuilder.Models;

using NuGet.Versioning;

namespace NReleaseBuilder.Abstractions.Bitbucket;

/// <summary>
/// Builds repository tag lookups enriched with Jira data.
/// </summary>
public interface IBitbucketTagLookupCore
{
    /// <summary>
    /// Builds a repository tag lookup for a repository.
    /// </summary>
    /// <param name="repository">Repository to inspect.</param>
    /// <param name="minCurrentVersion">Optional minimum version threshold.</param>
    /// <param name="progress">Optional progress callbacks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Repository lookup result.</returns>
    Task<RepositoryTagLookup> GetRepositoryTagLookupAsync(
        RepositoryName repository,
        NuGetVersion? minCurrentVersion,
        BitbucketProgressCallbacks? progress,
        CancellationToken cancellationToken);
}
