using System.ComponentModel.DataAnnotations;

namespace NReleaseBuilder.Configuration;

/// <summary>
/// Configuration settings for Bitbucket API access.
/// </summary>
public sealed class BitbucketOptions
{
    /// <summary>
    /// Base Bitbucket API URL.
    /// </summary>
    [Required]
    public required Uri BaseUrl { get; init; }

    /// <summary>
    /// Bitbucket workspace identifier.
    /// </summary>
    [Required]
    [MinLength(1)]
    public required string Workspace { get; init; }

    /// <summary>
    /// Jira project key used to extract tasks from commit messages.
    /// </summary>
    [Required]
    [MinLength(1)]
    public required string ProjectName { get; init; }

    /// <summary>
    /// Authentication email for Bitbucket API.
    /// </summary>
    [Required]
    [MinLength(1)]
    public required string AuthEmail { get; init; }

    /// <summary>
    /// Authentication API token for Bitbucket API.
    /// </summary>
    [Required]
    [MinLength(1)]
    public required string AuthApiToken { get; init; }

    /// <summary>
    /// Number of tags per page.
    /// </summary>
    [Range(1, 100)]
    public int PageLen { get; init; } = 50;

    /// <summary>
    /// Number of retries for transient Bitbucket errors.
    /// </summary>
    [Range(0, 10)]
    public int RetryCount { get; init; } = 2;

    /// <summary>
    /// Maximum number of parallel Bitbucket requests.
    /// </summary>
    [Range(1, 20)]
    public int MaxParallelRequests { get; init; } = 6;
}
