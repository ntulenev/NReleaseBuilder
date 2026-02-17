using System.Text.Json;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Models;

using NuGet.Versioning;

namespace NReleaseBuilder.Bitbucket;

/// <summary>
/// Batched repository tag lookup loader with progress reporting.
/// </summary>
public sealed class RepositoryTagLookupBatchLoader : IRepositoryTagLookupBatchLoader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryTagLookupBatchLoader"/> class.
    /// </summary>
    /// <param name="bitbucketTagClient">Bitbucket tag client.</param>
    /// <param name="renderer">Application renderer.</param>
    public RepositoryTagLookupBatchLoader(
        IBitbucketTagClient bitbucketTagClient,
        IRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(bitbucketTagClient);
        ArgumentNullException.ThrowIfNull(renderer);

        _bitbucketTagClient = bitbucketTagClient;
        _renderer = renderer;
    }

    /// <inheritdoc />
    public async Task<Dictionary<RepositoryName, RepositoryTagLookup>?> LoadAsync(
        IReadOnlyList<RepositoryName> repositories,
        IReadOnlyDictionary<RepositoryName, NuGetVersion> minCurrentVersionsByRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(minCurrentVersionsByRepository);

        try
        {
            var lookupsByRepository = new Dictionary<RepositoryName, RepositoryTagLookup>(repositories.Count);
            var repositoryBatches = repositories
                .Chunk(BITBUCKET_REPOSITORY_BATCH_SIZE)
                .ToArray();

            for (var batchIndex = 0; batchIndex < repositoryBatches.Length; batchIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batchRepositories = repositoryBatches[batchIndex];
                _renderer.PrintRepositoryBatchProgress(
                    batchIndex + 1,
                    repositoryBatches.Length,
                    lookupsByRepository.Count,
                    batchRepositories.Length,
                    repositories.Count);

                var batchLookups = await _renderer
                    .RunBitbucketLoadingWithProgressAsync(
                        batchRepositories,
                        progress => _bitbucketTagClient.FetchRepositoryTagLookupsAsync(
                            batchRepositories,
                            minCurrentVersionsByRepository,
                            progress,
                            cancellationToken))
                    .ConfigureAwait(false);

                foreach (var (repository, lookup) in batchLookups)
                {
                    lookupsByRepository[repository] = lookup;
                }
            }

            return lookupsByRepository;
        }
        catch (HttpRequestException ex)
        {
            PrintBitbucketLoadingError(ex);
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            PrintBitbucketLoadingError(ex);
            return null;
        }
        catch (JsonException ex)
        {
            PrintBitbucketLoadingError(ex);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            PrintBitbucketLoadingError(ex);
            return null;
        }
    }

    private void PrintBitbucketLoadingError(Exception exception)
    {
        _renderer.PrintError(
            new ErrorMessage($"Failed to load tags from Bitbucket: {exception.Message}"));
    }

    private const int BITBUCKET_REPOSITORY_BATCH_SIZE = 10;
    private readonly IBitbucketTagClient _bitbucketTagClient;
    private readonly IRenderer _renderer;
}
