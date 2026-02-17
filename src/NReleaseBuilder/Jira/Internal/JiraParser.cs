using System.Text.RegularExpressions;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Models;

namespace NReleaseBuilder.Jira.Internal;

/// <summary>
/// Default Jira task parser implementation.
/// </summary>
public sealed class JiraParser : IJiraParser
{
    /// <inheritdoc />
    public JiraTaskReference ExtractJiraTask(CommitInfo commitInfo, IReadOnlyList<JiraProjectName> projectNames)
    {
        ArgumentNullException.ThrowIfNull(commitInfo);
        ArgumentNullException.ThrowIfNull(projectNames);

        var commitMessage = commitInfo.Message;
        if (string.IsNullOrWhiteSpace(commitMessage) || projectNames.Count == 0)
        {
            return JiraTaskReference.NotAvailable;
        }

        var projectNamePattern = string.Join(
            "|",
            projectNames.Select(static projectName => Regex.Escape(projectName.Value)));
        var pattern = $@"\b(?<project>{projectNamePattern})-\d+\b";
        var matches = Regex.Matches(commitMessage, pattern, RegexOptions.IgnoreCase);

        if (matches.Count == 0)
        {
            return JiraTaskReference.NotAvailable;
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

        return new JiraTaskReference(string.Join(", ", jiraTasks));
    }

    /// <inheritdoc />
    public JiraTaskReference[] SplitJiraTasks(JiraTaskReference jiraTask)
    {
        if (string.Equals(jiraTask.Value, JiraTaskReference.NotAvailable.Value, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return
        [
            .. jiraTask.Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(static task => new JiraTaskReference(task))
        ];
    }

    /// <inheritdoc />
    public bool HasDependencyIssue(
        JiraTaskReference currentTask,
        JiraAlertDetails alertDetails,
        IReadOnlyList<JiraProjectName> projectNames)
    {
        ArgumentNullException.ThrowIfNull(projectNames);

        if (projectNames.Count == 0)
        {
            return false;
        }

        var details = string.Join(
            Environment.NewLine,
            new[] { alertDetails.RequiredActionsDetails, alertDetails.BreakingChangesDetails }
                .Where(static text => !string.IsNullOrWhiteSpace(text)));

        if (string.IsNullOrWhiteSpace(details))
        {
            return false;
        }

        var projectNamePattern = string.Join(
            "|",
            projectNames.Select(static projectName => Regex.Escape(projectName.Value)));
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
            if (!string.Equals(taskReference, currentTask.Value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
