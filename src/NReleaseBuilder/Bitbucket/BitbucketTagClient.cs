using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;

using Microsoft.Extensions.Options;

using NuGet.Versioning;

namespace NReleaseBuilder.Bitbucket;

/// <summary>
/// Bitbucket data loader for repository tags and Jira-enriched tag metadata.
/// </summary>
public sealed class BitbucketTagClient : IBitbucketTagClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BitbucketTagClient"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    /// <param name="bitbucketTagLookupCore">Bitbucket tag lookup abstraction.</param>
    public BitbucketTagClient(
        IOptions<AppSettings> options,
        IBitbucketTagLookupCore bitbucketTagLookupCore)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(bitbucketTagLookupCore);

        var appSettings = options.Value;

        _bitbucketTagLookupCore = bitbucketTagLookupCore;
        _bitbucketOptions = appSettings.Bitbucket;
    }

    /// <inheritdoc />
    public async Task<Dictionary<RepositoryName, RepositoryTagLookup>> FetchRepositoryTagLookupsAsync(
        IReadOnlyList<RepositoryName> repositories,
        IReadOnlyDictionary<RepositoryName, NuGetVersion> minCurrentVersionsByRepository,
        BitbucketProgressCallbacks? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(minCurrentVersionsByRepository);
        using var semaphore = new SemaphoreSlim(
            _bitbucketOptions.MaxParallelRequests,
            _bitbucketOptions.MaxParallelRequests);

        var tasks = repositories.Select(async repository =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                progress?.RepositoryStarted?.Invoke(repository.Value);

                _ = minCurrentVersionsByRepository.TryGetValue(repository, out var minCurrentVersion);
                var lookup = await _bitbucketTagLookupCore.GetRepositoryTagLookupAsync(
                        repository,
                        minCurrentVersion,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);

                return (Repository: repository, Lookup: lookup);
            }
            finally
            {
                progress?.RepositoryCompleted?.Invoke(repository.Value);
                _ = semaphore.Release();
            }
        }).ToArray();

        var pairs = await Task.WhenAll(tasks).ConfigureAwait(false);
        return pairs.ToDictionary(x => x.Repository, x => x.Lookup);
    }
    private readonly IBitbucketTagLookupCore _bitbucketTagLookupCore;
    private readonly BitbucketOptions _bitbucketOptions;
}
