

using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Abstractions.Jira;

/// <summary>
/// Resolves Jira task keys from commit messages and enriches them with Jira data.
/// </summary>
public interface IJiraTaskResolver
{
    /// <summary>
    /// Resolves Jira details for tasks found in a commit message.
    /// </summary>
    /// <param name="commitInfo">Commit payload that may include Jira task keys.</param>
    /// <param name="projectNames">Allowed Jira project keys used for task extraction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved Jira information for output rendering.</returns>
    Task<JiraTaskResolution> ResolveFromCommitMessageAsync(
        CommitInfo commitInfo,
        IReadOnlyList<JiraProjectName> projectNames,
        CancellationToken cancellationToken);

    /// <summary>
    /// Preloads Jira task metadata for tasks extracted from the provided commit payloads.
    /// </summary>
    /// <param name="commitInfos">Commit payloads that may include Jira task keys.</param>
    /// <param name="projectNames">Allowed Jira project keys used for task extraction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when preloading finishes.</returns>
    Task PrimeTaskInfoCacheAsync(
        IReadOnlyList<CommitInfo> commitInfos,
        IReadOnlyList<JiraProjectName> projectNames,
        CancellationToken cancellationToken);
}
