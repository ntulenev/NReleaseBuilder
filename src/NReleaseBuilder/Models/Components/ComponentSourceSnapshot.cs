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
}
