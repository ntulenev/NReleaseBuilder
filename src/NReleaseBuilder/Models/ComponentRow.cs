namespace NReleaseBuilder.Models;

/// <summary>
/// CSV input row describing component repository/version.
/// </summary>
/// <param name="Component">Component name.</param>
/// <param name="Repository">Repository name.</param>
/// <param name="Version">Current version from image tag.</param>
public readonly record struct ComponentRow(
    ComponentName Component,
    RepositoryName Repository,
    VersionLabel Version);
