namespace NReleaseBuilder.Models.Jira;

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

        if (issues.Any(static issue => issue is null))
        {
            throw new ArgumentException("Jira issues collection must not contain null items.", nameof(issues));
        }

        Issues = issues;
    }

    /// <summary>
    /// Resolved issues.
    /// </summary>
    public IReadOnlyList<JiraIssueInfo> Issues { get; }
}
