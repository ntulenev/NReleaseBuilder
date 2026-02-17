using System.Globalization;

using NReleaseBuilder.Models;

namespace NReleaseBuilder.Presentation;

/// <summary>
/// Shared helper extensions for presentation-layer formatting and aggregation.
/// </summary>
internal static class PresentationHelpers
{
    /// <summary>
    /// Builds a readable label for configured Jira status filters.
    /// </summary>
    /// <param name="statuses">Allowed Jira statuses.</param>
    /// <returns>User-facing label.</returns>
    public static string BuildStatusFilterLabel(this IReadOnlyList<JiraStatusName> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        return statuses.Count == 0
            ? "configured statuses"
            : string.Join(", ", statuses.Select(static x => x.Value));
    }

    /// <summary>
    /// Builds top disallowed Jira statuses with counters.
    /// </summary>
    /// <param name="statusStatistics">Status counters.</param>
    /// <param name="allowedStatuses">Allowed statuses.</param>
    /// <param name="maxItems">Maximum number of entries.</param>
    /// <returns>Sorted labels in <c>Status (Count)</c> format.</returns>
    public static string[] BuildTopDisallowedStatusLabels(
        this IReadOnlyDictionary<JiraStatusName, int> statusStatistics,
        IReadOnlyCollection<JiraStatusName> allowedStatuses,
        int maxItems = 8)
    {
        ArgumentNullException.ThrowIfNull(statusStatistics);
        ArgumentNullException.ThrowIfNull(allowedStatuses);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxItems, 1);

        var allowed = new HashSet<JiraStatusName>(allowedStatuses);
        return
        [
            .. statusStatistics
                .Where(x => !allowed.Contains(x.Key))
                .OrderByDescending(static x => x.Value)
                .ThenBy(static x => x.Key)
                .Take(maxItems)
                .Select(static x => string.Format(CultureInfo.InvariantCulture, "{0} ({1})", x.Key.Value, x.Value))
        ];
    }

    /// <summary>
    /// Filters rows by allowed Jira statuses.
    /// </summary>
    /// <param name="rows">Rows to filter.</param>
    /// <param name="allowedStatuses">Allowed Jira statuses.</param>
    /// <returns>Rows matching configured status filter.</returns>
    public static ComponentCheckRow[] FilterRowsByAllowedJiraStatuses(
        this IReadOnlyList<ComponentCheckRow> rows,
        JiraStatusName[] allowedStatuses)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(allowedStatuses);

        var allowed = new HashSet<JiraStatusName>(allowedStatuses);
        return allowed.Count == 0 ? [.. rows] : [.. rows.Where(row => row.MatchesStatusFilter(allowed))];
    }

    /// <summary>
    /// Checks whether a details field has meaningful text.
    /// </summary>
    /// <param name="value">Details value.</param>
    /// <returns><see langword="true"/> when details should be displayed.</returns>
    public static bool HasDetails(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !string.Equals(value.Trim(), "-", StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds merged task-level alert details from version rows.
    /// </summary>
    /// <param name="versions">Version rows.</param>
    /// <returns>Merged and ordered task details.</returns>
    public static JiraTaskAlertDetails[] BuildTaskAlertDetailsByTask(this IReadOnlyList<VersionJiraRow> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);

        var mergedByTask = new Dictionary<string, JiraTaskAlertDetails>(StringComparer.OrdinalIgnoreCase);

        foreach (var version in versions)
        {
            foreach (var taskDetail in version.TaskAlertDetails)
            {
                if (!mergedByTask.TryGetValue(taskDetail.Task.Value, out var existing))
                {
                    mergedByTask[taskDetail.Task.Value] = taskDetail;
                    continue;
                }

                mergedByTask[taskDetail.Task.Value] = MergeTaskAlertDetails(existing, taskDetail);
            }
        }

        return
        [
            .. mergedByTask
                .Values
                .OrderBy(static detail => detail.Task.Value, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>
    /// Builds unique Jira task counts grouped by Jira status.
    /// </summary>
    /// <param name="rows">Rows to analyze.</param>
    /// <returns>Status-to-unique-task-count mapping.</returns>
    public static Dictionary<JiraStatusName, int> BuildUniqueJiraTaskCountsByStatus(this IReadOnlyList<ComponentCheckRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var uniqueTasksByStatus = new Dictionary<JiraStatusName, HashSet<string>>();

        foreach (var row in rows)
        {
            foreach (var newerVersion in row.NewerVersions)
            {
                var taskKeys = SplitValues(newerVersion.JiraTask.Value);
                var statusNames = SplitValues(newerVersion.JiraStatus.Value);

                if (taskKeys.Length == 0 || statusNames.Length == 0)
                {
                    continue;
                }

                for (var i = 0; i < taskKeys.Length; i++)
                {
                    var taskKey = taskKeys[i];
                    if (!IsTrackableJiraTask(taskKey))
                    {
                        continue;
                    }

                    var statusName = new JiraStatusName(ResolveStatusName(statusNames, i));

                    if (!uniqueTasksByStatus.TryGetValue(statusName, out var taskSet))
                    {
#pragma warning disable IDE0028 // Simplify collection initialization
                        taskSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028 // Simplify collection initialization
                        uniqueTasksByStatus[statusName] = taskSet;
                    }

                    _ = taskSet.Add(taskKey);
                }
            }
        }

        return uniqueTasksByStatus.ToDictionary(static x => x.Key, static x => x.Value.Count);
    }

    /// <summary>
    /// Converts a check status to plain text representation.
    /// </summary>
    /// <param name="status">Check status value.</param>
    /// <returns>Status label.</returns>
    public static string ToPlainLabel(this CheckStatus status)
    {
        return status switch
        {
            CheckStatus.UpToDate => "Up to date",
            CheckStatus.Outdated => "Outdated",
            CheckStatus.RepositoryNotFound => "Repo not found",
            CheckStatus.BitbucketError => "Bitbucket error",
            CheckStatus.InvalidCurrentVersion => "Invalid version",
            _ => "Unknown",
        };
    }

    /// <summary>
    /// Builds a human-readable releases-ahead label.
    /// </summary>
    /// <param name="newerVersionCount">Count of newer releases.</param>
    /// <returns>Formatted label.</returns>
    public static string ToAheadReleasesLabel(this int newerVersionCount)
    {
        return newerVersionCount == 1
            ? "1 release ahead"
            : string.Format(CultureInfo.InvariantCulture, "{0} releases ahead", newerVersionCount);
    }

    private static JiraTaskAlertDetails MergeTaskAlertDetails(
        JiraTaskAlertDetails current,
        JiraTaskAlertDetails candidate)
    {
        var title = string.Equals(current.Title.Value, "N/A", StringComparison.OrdinalIgnoreCase)
            ? candidate.Title
            : current.Title;
        var status = string.Equals(current.Status.Value, "N/A", StringComparison.OrdinalIgnoreCase)
            ? candidate.Status
            : current.Status;
        var requiredActionsDetails = current.RequiredActionsDetails.HasDetails()
            ? current.RequiredActionsDetails
            : candidate.RequiredActionsDetails;
        var breakingChangesDetails = current.BreakingChangesDetails.HasDetails()
            ? current.BreakingChangesDetails
            : candidate.BreakingChangesDetails;

        return new JiraTaskAlertDetails(
            current.Task,
            title,
            status,
            requiredActionsDetails,
            breakingChangesDetails);
    }

    private static string[] SplitValues(string value) =>
    [
        .. value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
    ];

    private static string ResolveStatusName(string[] statusNames, int taskIndex)
    {
        if (statusNames.Length == 1)
        {
            return statusNames[0];
        }

        var statusIndex = taskIndex < statusNames.Length
            ? taskIndex
            : statusNames.Length - 1;
        return statusNames[statusIndex];
    }

    private static bool IsTrackableJiraTask(string taskKey)
    {
        if (string.Equals(taskKey, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dashIndex = taskKey.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex <= 0 || dashIndex == taskKey.Length - 1)
        {
            return false;
        }

        if (!char.IsLetter(taskKey[0]))
        {
            return false;
        }

        for (var i = 1; i < dashIndex; i++)
        {
            var symbol = taskKey[i];
            if (!char.IsLetterOrDigit(symbol) && symbol != '_')
            {
                return false;
            }
        }

        for (var i = dashIndex + 1; i < taskKey.Length; i++)
        {
            if (!char.IsDigit(taskKey[i]))
            {
                return false;
            }
        }

        return true;
    }
}
