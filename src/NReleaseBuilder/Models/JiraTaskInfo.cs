using System.Net;

namespace NReleaseBuilder.Models;

/// <summary>
/// Resolved Jira task metadata used during aggregation and caching.
/// </summary>
public readonly record struct JiraTaskInfo(
    string Status,
    string Title,
    string? RequiredActionsDetails,
    string? BreakingChangesDetails)
{
    /// <summary>
    /// Creates Jira task info for not-found Jira issues.
    /// </summary>
    /// <param name="statusFromSearch">Status resolved from search endpoint, if available.</param>
    /// <returns>Fallback Jira task info.</returns>
    public static JiraTaskInfo NotFound(string? statusFromSearch)
        => new(statusFromSearch ?? "Not found", "N/A", null, null);

    /// <summary>
    /// Creates Jira task info for non-success HTTP responses.
    /// </summary>
    /// <param name="statusCode">HTTP status code.</param>
    /// <returns>Fallback Jira task info.</returns>
    public static JiraTaskInfo HttpError(HttpStatusCode statusCode)
        => new($"HTTP {(int)statusCode}", "N/A", null, null);

    /// <summary>
    /// Gets a value indicating whether required actions text exists.
    /// </summary>
    public bool HasRequiredActions => !string.IsNullOrWhiteSpace(RequiredActionsDetails);

    /// <summary>
    /// Gets a value indicating whether breaking changes text exists.
    /// </summary>
    public bool HasBreakingChanges => !string.IsNullOrWhiteSpace(BreakingChangesDetails);

    /// <summary>
    /// Gets a value indicating whether the status corresponds to a transient HTTP error.
    /// </summary>
    /// <returns><see langword="true"/> when status is transient; otherwise <see langword="false"/>.</returns>
    public bool IsTransientStatus()
        => Status is "HTTP 408"
            or "HTTP 429"
            or "HTTP 500"
            or "HTTP 502"
            or "HTTP 503"
            or "HTTP 504";
}
