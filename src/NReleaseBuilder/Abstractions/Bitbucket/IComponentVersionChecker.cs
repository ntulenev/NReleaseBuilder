

using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Abstractions.Bitbucket;

/// <summary>
/// Builds per-component version check rows from source data.
/// </summary>
public interface IComponentVersionChecker
{
    /// <summary>
    /// Compares current component versions against repository tags.
    /// </summary>
    /// <param name="componentRows">Rows parsed from CSV.</param>
    /// <param name="tagLookups">Tag lookups keyed by repository name.</param>
    /// <returns>Calculated check rows for presentation.</returns>
    IReadOnlyList<ComponentCheckRow> BuildRows(
        IReadOnlyList<ComponentRow> componentRows,
        IReadOnlyDictionary<RepositoryName, RepositoryTagLookup> tagLookups);
}
