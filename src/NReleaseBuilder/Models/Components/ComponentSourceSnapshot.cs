using NReleaseBuilder.Configuration;

namespace NReleaseBuilder.Models.Components;

/// <summary>
/// Snapshot of component names detected in source CSV files.
/// </summary>
public sealed class ComponentSourceSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentSourceSnapshot"/> class.
    /// </summary>
    /// <param name="devComponents">Distinct components detected in development CSV.</param>
    /// <param name="targetComponents">Distinct components detected in target CSV.</param>
    public ComponentSourceSnapshot(
        IReadOnlyList<ComponentName> devComponents,
        IReadOnlyList<ComponentName> targetComponents)
    {
        ArgumentNullException.ThrowIfNull(devComponents);
        ArgumentNullException.ThrowIfNull(targetComponents);

        DevComponents = devComponents;
        TargetComponents = targetComponents;
    }

    /// <summary>
    /// Distinct components detected in development CSV.
    /// </summary>
    public IReadOnlyList<ComponentName> DevComponents { get; }

    /// <summary>
    /// Distinct components detected in target CSV.
    /// </summary>
    public IReadOnlyList<ComponentName> TargetComponents { get; }

    /// <summary>
    /// Builds rows describing presence differences across source CSVs and configured settings.
    /// </summary>
    /// <param name="settings">Application settings containing configured component names.</param>
    /// <returns>Difference rows for components that do not match across all sources.</returns>
    public IReadOnlyList<ComponentSourceDifferenceRow> BuildComponentSourceDifferenceRows(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var devComponents = DevComponents
            .Select(static x => x.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targetComponents = TargetComponents
            .Select(static x => x.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var configuredComponents = settings.BuildConfiguredComponentNames();

        var allComponents = devComponents
            .Concat(targetComponents)
            .Concat(configuredComponents)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = new List<ComponentSourceDifferenceRow>(allComponents.Length);

        foreach (var componentName in allComponents)
        {
            var isInDev = devComponents.Contains(componentName);
            var isInTarget = targetComponents.Contains(componentName);
            var isInSettings = configuredComponents.Contains(componentName);

            if (isInDev == isInTarget && isInTarget == isInSettings)
            {
                continue;
            }

            rows.Add(new ComponentSourceDifferenceRow(
                new ComponentName(componentName),
                isInDev,
                isInTarget,
                isInSettings));
        }

        return rows;
    }
}
