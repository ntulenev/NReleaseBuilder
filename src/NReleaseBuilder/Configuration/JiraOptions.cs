using System.ComponentModel.DataAnnotations;

using NReleaseBuilder.Models;

namespace NReleaseBuilder.Configuration;

/// <summary>
/// Configuration settings for Jira API access.
/// </summary>
public sealed class JiraOptions
{
    /// <summary>
    /// Base Jira URL.
    /// </summary>
    [Required]
    public required Uri BaseUrl { get; init; }

    /// <summary>
    /// Jira authentication email.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Jira authentication token.
    /// </summary>
    public string ApiToken { get; init; } = string.Empty;

    /// <summary>
    /// Allowed Jira statuses for all tasks attached to newer versions.
    /// </summary>
    public IReadOnlyList<string> AllowedTaskStatuses { get; init; } = [];

    /// <summary>
    /// Enables release alert checks for Jira custom fields.
    /// </summary>
    public bool CheckReleaseAlerts { get; init; }

    /// <summary>
    /// Jira custom field display name for release Required Actions.
    /// </summary>
    public string RequiredActionsFieldName { get; init; } = "Required Actions";

    /// <summary>
    /// Jira custom field display name for release Breaking changes.
    /// </summary>
    public string BreakingChangesFieldName { get; init; } = "Breaking changes";

    /// <summary>
    /// Number of retries for transient Jira errors.
    /// </summary>
    [Range(0, 10)]
    public int RetryCount { get; init; } = 2;

    /// <summary>
    /// Maximum number of parallel Jira requests.
    /// </summary>
    [Range(1, 20)]
    public int MaxParallelRequests { get; init; } = 2;

    /// <summary>
    /// Backward-compatible alias for <see cref="Email"/>.
    /// </summary>
    public string AuthEmail { get; init; } = string.Empty;

    /// <summary>
    /// Backward-compatible alias for <see cref="ApiToken"/>.
    /// </summary>
    public string AuthApiToken { get; init; } = string.Empty;

    /// <summary>
    /// Resolves email from current and backward-compatible fields.
    /// </summary>
    /// <returns>Trimmed authentication email value.</returns>
    public string ResolveAuthEmail()
    {
        if (!string.IsNullOrWhiteSpace(Email))
        {
            return Email.Trim();
        }

        return AuthEmail.Trim();
    }

    /// <summary>
    /// Resolves token from current and backward-compatible fields.
    /// </summary>
    /// <returns>Trimmed authentication token value.</returns>
    public string ResolveAuthApiToken()
    {
        if (!string.IsNullOrWhiteSpace(ApiToken))
        {
            return ApiToken.Trim();
        }

        return AuthApiToken.Trim();
    }

    /// <summary>
    /// Builds distinct allowed Jira statuses from configuration values.
    /// </summary>
    /// <returns>Distinct allowed Jira statuses.</returns>
    public JiraStatusName[] BuildAllowedStatuses()
    {
        ArgumentNullException.ThrowIfNull(AllowedTaskStatuses);

        return
        [
            .. AllowedTaskStatuses
                .Select(static status => new JiraStatusName(status))
                .Distinct()
        ];
    }
}
