using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Models;
using NReleaseBuilder.Services;

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
    /// <param name="repositoryTagLookupBatchLoader">Repository tag lookup batch loader.</param>
    /// <param name="versionChecker">Version comparison service.</param>
    /// <param name="renderer">Console renderer.</param>
    public VersionCheckApplication(
        ICsvComponentReader csvReader,
        IRepositoryTagLookupBatchLoader repositoryTagLookupBatchLoader,
        IComponentVersionChecker versionChecker,
        IConsoleRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(csvReader);
        ArgumentNullException.ThrowIfNull(repositoryTagLookupBatchLoader);
        ArgumentNullException.ThrowIfNull(versionChecker);
        ArgumentNullException.ThrowIfNull(renderer);

        _csvReader = csvReader;
        _repositoryTagLookupBatchLoader = repositoryTagLookupBatchLoader;
        _versionChecker = versionChecker;
        _renderer = renderer;
    }

    /// <inheritdoc />
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var componentRows = TryReadComponentRows()?.ToList();

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

        var tagLookups = await _repositoryTagLookupBatchLoader.LoadAsync(
            repositoryContext.Repositories,
            repositoryContext.MinCurrentVersionsByRepository,
            cancellationToken)
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
        => _csvReader.Read();

    private bool TryHandleNoComponentRows(List<ComponentRow> componentRows)
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

    private readonly record struct RepositoryVersionContext(
        RepositoryName[] Repositories,
        IReadOnlyDictionary<RepositoryName, NuGetVersion> MinCurrentVersionsByRepository);

    private readonly ICsvComponentReader _csvReader;
    private readonly IRepositoryTagLookupBatchLoader _repositoryTagLookupBatchLoader;
    private readonly IComponentVersionChecker _versionChecker;
    private readonly IConsoleRenderer _renderer;
}
