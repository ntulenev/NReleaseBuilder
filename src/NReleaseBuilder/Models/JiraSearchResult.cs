namespace NReleaseBuilder.Models;

/// <summary>
/// Jira search result domain model.
/// </summary>
public sealed class JiraSearchResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraSearchResult"/> class.
    /// </summary>
    /// <param name="issues">Resolved issues.</param>
    public JiraSearchResult(IReadOnlyList<JiraIssueInfo> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        Issues = issues;
    }

    /// <summary>
    /// Resolved issues.
    /// </summary>
    public IReadOnlyList<JiraIssueInfo> Issues { get; }
}
