using System.ComponentModel.DataAnnotations;

using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;



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
    /// Jira project keys used to extract tasks from commit messages.
    /// </summary>
    public IReadOnlyList<string> ProjectNames { get; init; } = [];

    /// <summary>
    /// Backward-compatible alias for <see cref="ProjectNames"/>.
    /// </summary>
    public string ProjectName { get; init; } = string.Empty;

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

    /// <summary>
    /// Enables fallback lookup that retries missing repositories without the last dot-separated segment.
    /// </summary>
    public bool UseTruncatedRepositoryNameFallback { get; init; }

    /// <summary>
    /// Optional mapping from CSV repository names to real Bitbucket repository names.
    /// </summary>
    public IReadOnlyDictionary<string, string> RepositoryNameOverrides { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Resolves project keys from current and backward-compatible fields.
    /// </summary>
    /// <returns>Distinct project keys.</returns>
    public JiraProjectName[] ResolveProjectNames()
    {
        if (ProjectNames.Count > 0)
        {
            return
            [
                .. ProjectNames
                    .Where(static x => !string.IsNullOrWhiteSpace(x))
                    .Select(static x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(static x => new JiraProjectName(x))
            ];
        }

        return string.IsNullOrWhiteSpace(ProjectName)
            ? []
            : [new JiraProjectName(ProjectName)];
    }

    /// <summary>
    /// Resolves repository name via optional override mapping.
    /// </summary>
    /// <param name="repository">Repository from CSV.</param>
    /// <returns>Mapped repository when configured; otherwise original repository.</returns>
    public RepositoryName ResolveRepositoryName(RepositoryName repository)
    {
        if (RepositoryNameOverrides.Count == 0)
        {
            return repository;
        }

        foreach (var (sourceRepositoryName, targetRepositoryName) in RepositoryNameOverrides)
        {
            if (string.Equals(sourceRepositoryName, repository.Value, StringComparison.OrdinalIgnoreCase))
            {
                return new RepositoryName(targetRepositoryName);
            }
        }

        return repository;
    }
}
