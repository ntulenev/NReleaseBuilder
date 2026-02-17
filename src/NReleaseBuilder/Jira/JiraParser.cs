using System.Text.RegularExpressions;

using NReleaseBuilder.Abstractions;

namespace NReleaseBuilder.Jira;

/// <summary>
/// Default Jira task parser implementation.
/// </summary>
public sealed class JiraParser : IJiraParser
{
    /// <inheritdoc />
    public string ExtractJiraTask(string? commitMessage, IReadOnlyList<string> projectNames)
    {
        ArgumentNullException.ThrowIfNull(projectNames);

        if (string.IsNullOrWhiteSpace(commitMessage) || projectNames.Count == 0)
        {
            return "N/A";
        }

        var projectNamePattern = string.Join(
            "|",
            projectNames.Select(static projectName => Regex.Escape(projectName)));
        var pattern = $@"\b(?<project>{projectNamePattern})-\d+\b";
        var matches = Regex.Matches(commitMessage, pattern, RegexOptions.IgnoreCase);

        if (matches.Count == 0)
        {
            return "N/A";
        }

        var selectedProjectName = matches[0].Groups["project"].Value;

        var jiraTasks = matches
            .Where(match => string.Equals(
                match.Groups["project"].Value,
                selectedProjectName,
                StringComparison.OrdinalIgnoreCase))
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join(", ", jiraTasks);
    }

    /// <inheritdoc />
    public string[] SplitJiraTasks(string jiraTask)
    {
        if (string.IsNullOrWhiteSpace(jiraTask) || string.Equals(jiraTask, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return
        [
            .. jiraTask
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <inheritdoc />
    public bool HasDependencyIssue(
        string currentTask,
        string? requiredActionsDetails,
        string? breakingChangesDetails,
        IReadOnlyList<string> projectNames)
    {
        ArgumentNullException.ThrowIfNull(projectNames);

        if (projectNames.Count == 0)
        {
            return false;
        }

        var details = string.Join(
            Environment.NewLine,
            new[] { requiredActionsDetails, breakingChangesDetails }
                .Where(static text => !string.IsNullOrWhiteSpace(text)));

        if (string.IsNullOrWhiteSpace(details))
        {
            return false;
        }

        var projectNamePattern = string.Join(
            "|",
            projectNames.Select(static projectName => Regex.Escape(projectName)));
        if (string.IsNullOrWhiteSpace(projectNamePattern))
        {
            return false;
        }

        var matches = Regex.Matches(details, $@"\b(?:{projectNamePattern})-\d+\b", RegexOptions.IgnoreCase);
        if (matches.Count == 0)
        {
            return false;
        }

        foreach (Match match in matches)
        {
            var taskReference = match.Value.ToUpperInvariant();
            if (!string.Equals(taskReference, currentTask, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
