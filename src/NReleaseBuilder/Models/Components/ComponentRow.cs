
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
    /// <param name="version">Current version from image tag.</param>
    public ComponentRow(ComponentName component, RepositoryName repository, VersionLabel version)
    {
        Component = component;
        Repository = repository;
        Version = version;
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
    /// Current version from image tag.
    /// </summary>
    public VersionLabel Version { get; }
}

