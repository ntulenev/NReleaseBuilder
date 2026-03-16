using NReleaseBuilder.Abstractions.Csv;
using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Csv;

/// <summary>
/// Merges target and development rows into the release result set.
/// </summary>
public sealed class CsvComponentRowsMerger : ICsvComponentRowsMerger
{
    /// <inheritdoc />
    public IReadOnlyList<ComponentRow> Merge(
        IReadOnlyDictionary<string, ComponentRow> targetRowsByComponent,
        IReadOnlyDictionary<string, ComponentRow> devRowsByComponent)
    {
        ArgumentNullException.ThrowIfNull(targetRowsByComponent);
        ArgumentNullException.ThrowIfNull(devRowsByComponent);

        var rows = new List<ComponentRow>(targetRowsByComponent.Count + devRowsByComponent.Count);

        foreach (var targetRow in targetRowsByComponent.Values)
        {
            rows.Add(targetRow);
        }

        foreach (var (componentName, devRow) in devRowsByComponent)
        {
            if (targetRowsByComponent.ContainsKey(componentName))
            {
                continue;
            }

            rows.Add(new ComponentRow(
                devRow.Component,
                devRow.Repository,
                devRow.Version,
                isReleased: false));
        }

        return rows;
    }
}
