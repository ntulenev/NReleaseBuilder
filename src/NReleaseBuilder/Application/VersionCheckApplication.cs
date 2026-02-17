using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Models;

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
    /// <param name="renderer">Application renderer.</param>
    public VersionCheckApplication(
        ICsvComponentReader csvReader,
        IRepositoryTagLookupBatchLoader repositoryTagLookupBatchLoader,
        IComponentVersionChecker versionChecker,
        IRenderer renderer)
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

        var repositoryContext = RepositoryVersionContext.BuildRepositoryVersionContext(componentRows);
        _renderer.PrintRepositoryCheckCount(repositoryContext.Repositories.Count);

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

    private readonly ICsvComponentReader _csvReader;
    private readonly IRepositoryTagLookupBatchLoader _repositoryTagLookupBatchLoader;
    private readonly IComponentVersionChecker _versionChecker;
    private readonly IRenderer _renderer;
}
