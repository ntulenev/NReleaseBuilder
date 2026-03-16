
using NReleaseBuilder.Models.Bitbucket;

namespace NReleaseBuilder.Models.Components;

/// <summary>
/// CSV input row describing component repository/version.
/// </summary>
public readonly record struct ComponentRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentRow"/> struct.
    /// </summary>
    /// <param name="component">Component name.</param>
    /// <param name="repository">Repository name.</param>
    /// <param name="version">Version from image tag.</param>
    /// <param name="isReleased">Whether the version is already released in the target environment.</param>
    public ComponentRow(
        ComponentName component,
        RepositoryName repository,
        VersionLabel version,
        bool isReleased = true)
    {
        Component = component;
        Repository = repository;
        Version = version;
        IsReleased = isReleased;
    }

    /// <summary>
    /// Component name.
    /// </summary>
    public ComponentName Component { get; }

    /// <summary>
    /// Repository name.
    /// </summary>
    public RepositoryName Repository { get; }

    /// <summary>
    /// Version from image tag.
    /// </summary>
    public VersionLabel Version { get; }

    /// <summary>
    /// Whether the component version is already released in the target environment.
    /// </summary>
    public bool IsReleased { get; }
}

