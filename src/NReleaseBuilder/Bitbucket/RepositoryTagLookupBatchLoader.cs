using System.Text.Json;

using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Models;
using NReleaseBuilder.Models.Bitbucket;

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
            cancellationToken.ThrowIfCancellationRequested();

            _renderer.PrintRepositoryBatchProgress(
                batchNumber: 1,
                totalBatchCount: 1,
                processedRepositoryCount: 0,
                currentBatchRepositoryCount: repositories.Count,
                totalRepositoryCount: repositories.Count);

            return await _renderer
                .RunBitbucketLoadingWithProgressAsync(
                    repositories,
                    progress => _bitbucketTagClient.FetchRepositoryTagLookupsAsync(
                        repositories,
                        minCurrentVersionsByRepository,
                        progress,
                        cancellationToken))
                .ConfigureAwait(false);
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
    private readonly IBitbucketTagClient _bitbucketTagClient;
    private readonly IRenderer _renderer;
}
