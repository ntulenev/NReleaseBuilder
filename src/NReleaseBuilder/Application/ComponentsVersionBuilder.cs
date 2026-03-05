using NReleaseBuilder.Abstractions.Application;
using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Application;

/// <summary>
/// Coordinates repository tag loading and component check row construction.
/// </summary>
public sealed class ComponentsVersionBuilder : IComponentsVersionBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentsVersionBuilder"/> class.
    /// </summary>
    /// <param name="repositoryTagLookupBatchLoader">Repository tag lookup batch loader.</param>
    /// <param name="versionChecker">Version comparison service.</param>
    public ComponentsVersionBuilder(
        IRepositoryTagLookupBatchLoader repositoryTagLookupBatchLoader,
        IComponentVersionChecker versionChecker)
    {
        ArgumentNullException.ThrowIfNull(repositoryTagLookupBatchLoader);
        ArgumentNullException.ThrowIfNull(versionChecker);

        _repositoryTagLookupBatchLoader = repositoryTagLookupBatchLoader;
        _versionChecker = versionChecker;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ComponentCheckRow>?> BuildAsync(
        IReadOnlyList<ComponentRow> normalizedComponentRows,
        RepositoryVersionContext repositoryContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(normalizedComponentRows);
        ArgumentNullException.ThrowIfNull(repositoryContext);

        var tagLookups = await _repositoryTagLookupBatchLoader.LoadAsync(
            repositoryContext.Repositories,
            repositoryContext.MinCurrentVersionsByRepository,
            cancellationToken)
            .ConfigureAwait(false);

        if (tagLookups is null)
        {
            return null;
        }

        return _versionChecker.BuildRows(normalizedComponentRows, tagLookups);
    }

    private readonly IRepositoryTagLookupBatchLoader _repositoryTagLookupBatchLoader;
    private readonly IComponentVersionChecker _versionChecker;
}
