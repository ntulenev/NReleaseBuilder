namespace NReleaseBuilder.Models.Components;

/// <summary>
/// Result status of component version check.
/// </summary>
public enum CheckStatus
{
    /// <summary>
    /// Current version is up to date.
    /// </summary>
    UpToDate,

    /// <summary>
    /// Newer versions were found.
    /// </summary>
    Outdated,

    /// <summary>
    /// Repository was not found in Bitbucket workspace.
    /// </summary>
    RepositoryNotFound,

    /// <summary>
    /// Bitbucket API returned an error.
    /// </summary>
    BitbucketError,

    /// <summary>
    /// Current version from CSV is invalid.
    /// </summary>
    InvalidCurrentVersion,
}

