using System.Text.Json;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Models;
using NReleaseBuilder.Services;

using Microsoft.VisualBasic.FileIO;

using NuGet.Versioning;

namespace NReleaseBuilder.Application;

/// <summary>
/// Coordinates end-to-end component version checks and console output.
/// </summary>
public sealed class VersionCheckApplication : IVersionCheckApplication
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersionCheckApplication"/> class.
    /// </summary>
    /// <param name="csvReader">CSV reader service.</param>
    /// <param name="bitbucketTagClient">Bitbucket tag client.</param>
    /// <param name="versionChecker">Version comparison service.</param>
    /// <param name="renderer">Console renderer.</param>
    public VersionCheckApplication(
        ICsvComponentReader csvReader,
        IBitbucketTagClient bitbucketTagClient,
        IComponentVersionChecker versionChecker,
        IConsoleRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(csvReader);
        ArgumentNullException.ThrowIfNull(bitbucketTagClient);
        ArgumentNullException.ThrowIfNull(versionChecker);
        ArgumentNullException.ThrowIfNull(renderer);

        _csvReader = csvReader;
        _bitbucketTagClient = bitbucketTagClient;
        _versionChecker = versionChecker;
        _renderer = renderer;
    }

    /// <inheritdoc />
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var componentRows = TryReadComponentRows();

        if (componentRows is null)
        {
            return 1;
        }

        _renderer.RenderHeader();

        if (TryHandleNoComponentRows(componentRows))
        {
            return 0;
        }

        var repositoryContext = BuildRepositoryVersionContext(componentRows);
        _renderer.PrintRepositoryCheckCount(repositoryContext.Repositories.Length);

        var tagLookups = await TryLoadTagLookupsAsync(repositoryContext, cancellationToken)
            .ConfigureAwait(false);

        if (tagLookups is null)
        {
            return 1;
        }

        var checkRows = _versionChecker.BuildRows(componentRows, tagLookups);
        _renderer.RenderResults(checkRows);
        return 0;
    }

    private IReadOnlyList<ComponentRow>? TryReadComponentRows()
    {
        try
        {
            return _csvReader.Read();
        }
        catch (MalformedLineException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
        catch (IOException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
        catch (ArgumentException ex)
        {
            PrintCsvParsingError(ex);
            return null;
        }
    }

    private bool TryHandleNoComponentRows(IReadOnlyList<ComponentRow> componentRows)
    {
        if (componentRows.Count != 0)
        {
            return false;
        }

        _renderer.PrintNoRows();
        return true;
    }

    private static RepositoryVersionContext BuildRepositoryVersionContext(IReadOnlyList<ComponentRow> componentRows)
    {
        var repositories = componentRows
            .Select(static row => row.Repository)
            .Distinct()
            .ToArray();

        var minCurrentVersionsByRepository = BuildMinCurrentVersionsByRepository(componentRows);

        return new RepositoryVersionContext(repositories, minCurrentVersionsByRepository);
    }

    private static Dictionary<RepositoryName, NuGetVersion> BuildMinCurrentVersionsByRepository(
        IReadOnlyList<ComponentRow> componentRows)
    {
        var minCurrentVersionsByRepository = new Dictionary<RepositoryName, NuGetVersion>();

        foreach (var row in componentRows)
        {
            if (!VersionParser.TryParse(row.Version, out var parsedVersion))
            {
                continue;
            }

            var repositoryName = row.Repository;

            if (!minCurrentVersionsByRepository.TryGetValue(repositoryName, out var minVersion)
                || parsedVersion < minVersion)
            {
                minCurrentVersionsByRepository[repositoryName] = parsedVersion;
            }
        }

        return minCurrentVersionsByRepository;
    }

    private async Task<Dictionary<RepositoryName, RepositoryTagLookup>?> TryLoadTagLookupsAsync(
        RepositoryVersionContext repositoryContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var repositories = repositoryContext.Repositories;
            var lookupsByRepository = new Dictionary<RepositoryName, RepositoryTagLookup>(repositories.Length);
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
                    repositories.Length);

                var batchLookups = await _renderer
                    .RunBitbucketLoadingWithProgressAsync(
                        batchRepositories,
                        progress => _bitbucketTagClient.FetchRepositoryTagLookupsAsync(
                            batchRepositories,
                            repositoryContext.MinCurrentVersionsByRepository,
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

    private void PrintCsvParsingError(Exception exception)
        => _renderer.PrintError(new ErrorMessage($"Failed to parse CSV: {exception.Message}"));

    private void PrintBitbucketLoadingError(Exception exception)
        => _renderer.PrintError(new ErrorMessage($"Failed to load tags from Bitbucket: {exception.Message}"));

    private readonly record struct RepositoryVersionContext(
        RepositoryName[] Repositories,
        IReadOnlyDictionary<RepositoryName, NuGetVersion> MinCurrentVersionsByRepository);

    private const int BITBUCKET_REPOSITORY_BATCH_SIZE = 10;
    private readonly ICsvComponentReader _csvReader;
    private readonly IBitbucketTagClient _bitbucketTagClient;
    private readonly IComponentVersionChecker _versionChecker;
    private readonly IConsoleRenderer _renderer;
}
