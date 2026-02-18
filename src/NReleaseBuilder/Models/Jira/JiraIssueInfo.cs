namespace NReleaseBuilder.Models.Jira;

/// <summary>
/// Jira issue domain model with resolved status and title.
/// </summary>
public sealed class JiraIssueInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraIssueInfo"/> class.
    /// </summary>
    /// <param name="statusName">Resolved status name.</param>
    /// <param name="title">Resolved issue title.</param>
    /// <param name="requiredActionsDetails">Required Actions details text.</param>
    /// <param name="breakingChangesDetails">Breaking changes details text.</param>
    public JiraIssueInfo(
        JiraStatusName? statusName,
        string title,
        string? requiredActionsDetails,
        string? breakingChangesDetails)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        StatusName = statusName;
        Title = title.Trim();
        RequiredActionsDetails = Normalize(requiredActionsDetails);
        BreakingChangesDetails = Normalize(breakingChangesDetails);
    }

    /// <summary>
    /// Resolved issue status name.
    /// </summary>
    public JiraStatusName? StatusName { get; }

    /// <summary>
    /// Resolved issue title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Required Actions details text.
    /// </summary>
    public string? RequiredActionsDetails { get; }

    /// <summary>
    /// Breaking changes details text.
    /// </summary>
    public string? BreakingChangesDetails { get; }

    /// <summary>
    /// Whether issue has non-empty Required Actions field.
    /// </summary>
    public bool HasRequiredActions => !string.IsNullOrWhiteSpace(RequiredActionsDetails);

    /// <summary>
    /// Whether issue has non-empty Breaking changes field.
    /// </summary>
    public bool HasBreakingChanges => !string.IsNullOrWhiteSpace(BreakingChangesDetails);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
