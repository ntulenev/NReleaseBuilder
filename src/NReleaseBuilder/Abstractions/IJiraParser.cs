namespace NReleaseBuilder.Abstractions;

/// <summary>
/// Parses Jira task references from commit messages and alert details.
/// </summary>
public interface IJiraParser
{
    /// <summary>
    /// Extracts Jira task keys from a commit message.
    /// </summary>
    /// <param name="commitMessage">Commit message that may include Jira task keys.</param>
    /// <param name="projectNames">Allowed Jira project keys.</param>
    /// <returns>Comma-separated task keys or <c>N/A</c> when none are found.</returns>
    string ExtractJiraTask(string? commitMessage, IReadOnlyList<string> projectNames);

    /// <summary>
    /// Splits a comma-separated Jira task string into unique task keys.
    /// </summary>
    /// <param name="jiraTask">Comma-separated Jira task keys.</param>
    /// <returns>Distinct Jira task keys.</returns>
    string[] SplitJiraTasks(string jiraTask);

    /// <summary>
    /// Determines whether alert details reference Jira tasks from other project items.
    /// </summary>
    /// <param name="currentTask">Current Jira task key.</param>
    /// <param name="requiredActionsDetails">Required actions details text.</param>
    /// <param name="breakingChangesDetails">Breaking changes details text.</param>
    /// <param name="projectNames">Allowed Jira project keys.</param>
    /// <returns><see langword="true"/> when another Jira task is referenced; otherwise <see langword="false"/>.</returns>
    bool HasDependencyIssue(
        string currentTask,
        string? requiredActionsDetails,
        string? breakingChangesDetails,
        IReadOnlyList<string> projectNames);
}
