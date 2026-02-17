using NReleaseBuilder.Models;

namespace NReleaseBuilder.Abstractions.Jira;

/// <summary>
/// Parses Jira task references from commit messages and alert details.
/// </summary>
public interface IJiraParser
{
    /// <summary>
    /// Extracts Jira task keys from a commit message.
    /// </summary>
    /// <param name="commitInfo">Commit payload that may include Jira task keys.</param>
    /// <param name="projectNames">Allowed Jira project keys.</param>
    /// <returns>Extracted Jira task reference.</returns>
    JiraTaskReference ExtractJiraTask(CommitInfo commitInfo, IReadOnlyList<JiraProjectName> projectNames);

    /// <summary>
    /// Splits a comma-separated Jira task string into unique task keys.
    /// </summary>
    /// <param name="jiraTask">Jira task reference.</param>
    /// <returns>Distinct Jira task keys.</returns>
    JiraTaskReference[] SplitJiraTasks(JiraTaskReference jiraTask);

    /// <summary>
    /// Determines whether alert details reference Jira tasks from other project items.
    /// </summary>
    /// <param name="currentTask">Current Jira task key.</param>
    /// <param name="alertDetails">Required actions and breaking changes details.</param>
    /// <param name="projectNames">Allowed Jira project keys.</param>
    /// <returns><see langword="true"/> when another Jira task is referenced; otherwise <see langword="false"/>.</returns>
    bool HasDependencyIssue(
        JiraTaskReference currentTask,
        JiraAlertDetails alertDetails,
        IReadOnlyList<JiraProjectName> projectNames);
}
