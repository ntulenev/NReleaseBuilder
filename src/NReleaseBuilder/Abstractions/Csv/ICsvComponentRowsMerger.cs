using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Abstractions.Csv;

/// <summary>
/// Merges target and development component rows into a release view.
/// </summary>
public interface ICsvComponentRowsMerger
{
    /// <summary>
    /// Merges target rows with development rows.
    /// </summary>
    /// <param name="targetRowsByComponent">Target rows keyed by component name.</param>
    /// <param name="devRowsByComponent">Development rows keyed by component name.</param>
    /// <returns>Merged rows.</returns>
    IReadOnlyList<ComponentRow> Merge(
        IReadOnlyDictionary<string, ComponentRow> targetRowsByComponent,
        IReadOnlyDictionary<string, ComponentRow> devRowsByComponent);
}
