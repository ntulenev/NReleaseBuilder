namespace NReleaseBuilder.Models.Jira;

/// <summary>
/// Jira alert details used for dependency checks.
/// </summary>
public readonly record struct JiraAlertDetails
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraAlertDetails"/> struct.
    /// </summary>
    /// <param name="requiredActionsDetails">Required actions details text.</param>
    /// <param name="breakingChangesDetails">Breaking changes details text.</param>
    public JiraAlertDetails(string? requiredActionsDetails, string? breakingChangesDetails)
    {
        RequiredActionsDetails = NormalizeOptional(requiredActionsDetails);
        BreakingChangesDetails = NormalizeOptional(breakingChangesDetails);
    }

    /// <summary>
    /// Required actions details text.
    /// </summary>
    public string? RequiredActionsDetails { get; }

    /// <summary>
    /// Breaking changes details text.
    /// </summary>
    public string? BreakingChangesDetails { get; }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
