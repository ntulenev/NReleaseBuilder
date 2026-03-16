namespace NReleaseBuilder.Models.Components;

/// <summary>
/// Presence comparison row across development CSV, target CSV, and configured settings.
/// </summary>
public readonly record struct ComponentSourceDifferenceRow(
    ComponentName Component,
    bool IsInDev,
    bool IsInTarget,
    bool IsInSettings);
