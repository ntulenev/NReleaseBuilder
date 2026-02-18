using NReleaseBuilder.Jira.Internal.Models;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Abstractions.Jira;

/// <summary>
/// Jira integration operations for loading Jira task metadata from Jira API.
/// </summary>
public interface IJiraIntegrationCore
{
    /// <summary>
    /// Loads Jira task metadata for a Jira task reference.
    /// </summary>
    /// <param name="jiraTask">Jira task reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved Jira task metadata.</returns>
    Task<JiraTaskInfo> TryGetJiraTaskInfoAsync(
        JiraTaskReference jiraTask,
        CancellationToken cancellationToken);
}
