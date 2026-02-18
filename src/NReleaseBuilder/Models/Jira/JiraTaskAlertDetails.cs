namespace NReleaseBuilder.Models.Jira;

/// <summary>
/// Per-task Jira release alert details.
/// </summary>
public readonly record struct JiraTaskAlertDetails
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraTaskAlertDetails"/> struct.
    /// </summary>
    /// <param name="task">Jira task key.</param>
    /// <param name="title">Jira task title.</param>
    /// <param name="status">Jira task status.</param>
    /// <param name="requiredActionsDetails">Required Actions details text.</param>
    /// <param name="breakingChangesDetails">Breaking changes details text.</param>
    public JiraTaskAlertDetails(
        JiraTaskReference task,
        JiraTitleReference title,
        JiraStatusReference status,
        string? requiredActionsDetails,
        string? breakingChangesDetails)
    {
        Task = task;
        Title = title;
        Status = status;
        RequiredActionsDetails = Normalize(requiredActionsDetails);
        BreakingChangesDetails = Normalize(breakingChangesDetails);
    }

    /// <summary>
    /// Jira task key.
    /// </summary>
    public JiraTaskReference Task { get; }

    /// <summary>
    /// Jira task title.
    /// </summary>
    public JiraTitleReference Title { get; }

    /// <summary>
    /// Jira task status.
    /// </summary>
    public JiraStatusReference Status { get; }

    /// <summary>
    /// Required Actions details text.
    /// </summary>
    public string? RequiredActionsDetails { get; }

    /// <summary>
    /// Breaking changes details text.
    /// </summary>
    public string? BreakingChangesDetails { get; }

    /// <summary>
    /// Whether Required Actions details are present.
    /// </summary>
    public bool HasRequiredActions => !string.IsNullOrWhiteSpace(RequiredActionsDetails);

    /// <summary>
    /// Whether Breaking changes details are present.
    /// </summary>
    public bool HasBreakingChanges => !string.IsNullOrWhiteSpace(BreakingChangesDetails);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
