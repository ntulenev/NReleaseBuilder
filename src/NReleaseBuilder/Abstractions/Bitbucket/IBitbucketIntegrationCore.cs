using NReleaseBuilder.Bitbucket.Internal.Models;
using NReleaseBuilder.Models.Bitbucket;

namespace NReleaseBuilder.Abstractions.Bitbucket;

/// <summary>
/// Bitbucket integration operations for loading tags and commit metadata.
/// </summary>
public interface IBitbucketIntegrationCore
{
    /// <summary>
    /// Loads repository tag references from Bitbucket.
    /// </summary>
    /// <param name="repository">Repository name used in Bitbucket API calls.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Loaded tag references or error result.</returns>
    Task<RepositoryTagReferenceLoadResult> LoadRepositoryTagReferencesAsync(
        RepositoryName repository,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads commit message metadata from Bitbucket.
    /// </summary>
    /// <param name="repository">Repository name used in Bitbucket API calls.</param>
    /// <param name="commitHash">Commit hash.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Commit info, or empty commit info when request fails.</returns>
    Task<CommitInfo> TryGetCommitMessageAsync(
        RepositoryName repository,
        CommitHash commitHash,
        CancellationToken cancellationToken);
}
