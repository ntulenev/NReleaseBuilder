using System.Text.RegularExpressions;

using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Presentation;

/// <summary>
/// Shared parsing helpers for Jira task keys and Jira browse links in presentation layer.
/// </summary>
internal static partial class JiraTaskParsingHelpers
{
    /// <summary>
    /// Splits a Jira task reference string into non-empty trimmed task values.
    /// </summary>
    /// <param name="value">Raw Jira task reference value.</param>
    /// <returns>Task values.</returns>
    public static string[] SplitTaskValues(string value) =>
    [
        .. value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
    ];

    /// <summary>
    /// Checks whether provided task key can be treated as a Jira task reference.
    /// </summary>
    /// <param name="taskKey">Task key.</param>
    /// <returns><see langword="true"/> when task key is trackable.</returns>
    public static bool IsTrackableJiraTask(string taskKey)
    {
        ArgumentNullException.ThrowIfNull(taskKey);

        if (string.Equals(taskKey, JiraTaskReference.NotAvailable.Value, StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// Finds Jira browse links in arbitrary text.
    /// </summary>
    /// <param name="value">Source text.</param>
    /// <returns>Regex matches.</returns>
    public static MatchCollection MatchJiraBrowseUrls(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JiraBrowseUrlRegex().Matches(value);
    }

    /// <summary>
    /// Finds Jira task key mentions in arbitrary text.
    /// </summary>
    /// <param name="value">Source text.</param>
    /// <returns>Regex matches.</returns>
    public static MatchCollection MatchJiraTaskKeys(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JiraTaskKeyRegex().Matches(value);
    }

    [GeneratedRegex(
        @"https?://[^\s\)\]\}<>""']+/browse/(?<task>[A-Za-z][A-Za-z0-9_]*-\d+)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex JiraBrowseUrlRegex();

    [GeneratedRegex(
        @"(?<![A-Za-z0-9_])(?<task>[A-Za-z][A-Za-z0-9_]*-\d+)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant)]
    private static partial Regex JiraTaskKeyRegex();
}
