using System.Globalization;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;

using Spectre.Console;

namespace NReleaseBuilder.Presentation;

/// <summary>
/// Spectre.Console implementation for console-only rendering.
/// </summary>
public sealed class SpectreConsoleOutputRenderer : IConsoleOutputRenderer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreConsoleOutputRenderer"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    public SpectreConsoleOutputRenderer(IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _settings = options.Value;
    }

    /// <inheritdoc />
    public void RenderHeader()
    {
        AnsiConsole.Write(new Rule("[bold deepskyblue1]Components Version Check[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.MarkupLine($"[grey]Source:[/] [silver]{Markup.Escape(_settings.CsvFilePath)}[/]");
        AnsiConsole.MarkupLine($"[grey]Workspace:[/] [silver]{Markup.Escape(_settings.Bitbucket.Workspace)}[/]");
        if (_settings.Jira.AllowedTaskStatuses.Count > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Jira Status Filter:[/] [silver]{Markup.Escape(string.Join(", ", _settings.Jira.AllowedTaskStatuses))}[/]");
        }

        AnsiConsole.WriteLine();
    }

    /// <inheritdoc />
    public void PrintRepositoryCheckCount(int repositoryCount) =>
        AnsiConsole.MarkupLine($"[Grey]Checking Bitbucket tags for {repositoryCount} repositories...[/]");

    /// <inheritdoc />
    public void PrintRepositoryBatchProgress(
        int batchNumber,
        int totalBatchCount,
        int processedRepositoryCount,
        int currentBatchRepositoryCount,
        int totalRepositoryCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalBatchCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(processedRepositoryCount, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(currentBatchRepositoryCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalRepositoryCount, 1);

        var from = processedRepositoryCount + 1;
        var to = Math.Min(processedRepositoryCount + currentBatchRepositoryCount, totalRepositoryCount);

        AnsiConsole.MarkupLine(
            $"[grey]Batch {batchNumber}/{totalBatchCount}: loading repositories {from}-{to} of {totalRepositoryCount}[/]");
    }

    /// <inheritdoc />
    public async Task<T> RunBitbucketLoadingWithProgressAsync<T>(
        IReadOnlyList<RepositoryName> repositories,
        Func<BitbucketProgressCallbacks, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(operation);

        T? result = default;

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(),
                new RemainingTimeColumn())
            .StartAsync(async context =>
            {
                var sync = new object();
                var escapedRepositories = repositories
                    .Distinct()
                    .ToArray();

                var repositoryOverallTask = context.AddTask(
                    "[yellow]Repositories[/]",
                    maxValue: Math.Max(1, escapedRepositories.Length));

                var perRepositoryTasks = new Dictionary<string, ProgressTask>(StringComparer.OrdinalIgnoreCase);

                ProgressTask GetOrCreateRepositoryTask(string repository)
                {
                    lock (sync)
                    {
                        if (perRepositoryTasks.TryGetValue(repository, out var existing))
                        {
                            return existing;
                        }

                        var escapedRepositoryName = Markup.Escape(repository);
                        var task = context.AddTask($"[grey]{escapedRepositoryName}[/]", maxValue: 1);
                        task.IsIndeterminate = true;
                        perRepositoryTasks[repository] = task;
                        return task;
                    }
                }

                foreach (var repository in escapedRepositories)
                {
                    _ = GetOrCreateRepositoryTask(repository.Value);
                }

                var callbacks = new BitbucketProgressCallbacks
                {
                    RepositoryStarted = repository =>
                    {
                        var task = GetOrCreateRepositoryTask(repository);
                        lock (sync)
                        {
                            task.Description = $"[grey]{Markup.Escape(repository)}: loading tags[/]";
                            task.IsIndeterminate = true;
                        }
                    },
                    CommitTotalDetected = (repository, commitCount) =>
                    {
                        var task = GetOrCreateRepositoryTask(repository);
                        lock (sync)
                        {
                            if (commitCount <= 0)
                            {
                                task.IsIndeterminate = false;
                                task.MaxValue = 1;
                                task.Value = 1;
                                task.Description = $"[grey]{Markup.Escape(repository)}: no tags[/]";
                                return;
                            }

                            task.IsIndeterminate = false;
                            task.MaxValue = commitCount;
                            task.Value = 0;
                            task.Description = $"[grey]{Markup.Escape(repository)} commits 0/{commitCount}[/]";
                        }
                    },
                    CommitProcessed = repository =>
                    {
                        var task = GetOrCreateRepositoryTask(repository);
                        lock (sync)
                        {
                            if (task.IsIndeterminate)
                            {
                                return;
                            }

                            task.Increment(1);
                            var value = (int)Math.Min(task.MaxValue, task.Value);
                            task.Description =
                                $"[grey]{Markup.Escape(repository)} commits {value}/{(int)task.MaxValue}[/]";
                        }
                    },
                    RepositoryCompleted = repository =>
                    {
                        var task = GetOrCreateRepositoryTask(repository);
                        lock (sync)
                        {
                            if (task.IsIndeterminate)
                            {
                                task.IsIndeterminate = false;
                                task.MaxValue = 1;
                            }

                            task.Value = task.MaxValue;
                            task.StopTask();
                            task.Description = $"[green]{Markup.Escape(repository)} done[/]";
                            repositoryOverallTask.Increment(1);
                        }
                    },
                };

                result = await operation(callbacks).ConfigureAwait(false);
            }).ConfigureAwait(false);

        return result is not null
            ? result
            : throw new InvalidOperationException("Operation completed without returning a result.");
    }

    /// <inheritdoc />
    public void PrintNoRows() => AnsiConsole.MarkupLine("[yellow]No rows found in CSV.[/]");

    /// <inheritdoc />
    public void PrintNoComponentsMatchedStatusFilter(IReadOnlyList<JiraStatusName> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        var label = statuses.Count == 0
            ? "configured statuses"
            : string.Join(", ", statuses.Select(static x => x.Value));
        AnsiConsole.MarkupLine($"[yellow]No components matched Jira status filter:[/] [grey]{Markup.Escape(label)}[/]");
    }

    /// <inheritdoc />
    public void PrintStatusFilterDiagnostics(
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics,
        IReadOnlyList<JiraStatusName> allowedStatuses)
    {
        ArgumentNullException.ThrowIfNull(statusStatistics);
        ArgumentNullException.ThrowIfNull(allowedStatuses);

        if (statusStatistics.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No Jira statuses were resolved for newer versions.[/]");
            return;
        }

        var allowed = new HashSet<JiraStatusName>(allowedStatuses);

        var topDisallowed = statusStatistics
            .Where(x => !allowed.Contains(x.Key))
            .OrderByDescending(static x => x.Value)
            .ThenBy(static x => x.Key)
            .Take(8)
            .Select(static x => string.Format(CultureInfo.InvariantCulture, "{0} ({1})", x.Key.Value, x.Value))
            .ToArray();

        if (topDisallowed.Length == 0)
        {
            AnsiConsole.MarkupLine("[grey]All collected statuses are allowed, but no component passed the all-tasks rule.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[grey]Top disallowed statuses:[/] {Markup.Escape(string.Join(", ", topDisallowed))}");
    }

    /// <inheritdoc />
    public void RenderTable(IReadOnlyList<ComponentCheckRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Expand();

        _ = table.AddColumn(new TableColumn("[bold]#[/]").RightAligned());
        _ = table.AddColumn(new TableColumn("[bold deepskyblue1]Component[/]"));
        _ = table.AddColumn(new TableColumn("[bold springgreen2]Repository[/]"));
        _ = table.AddColumn(new TableColumn("[bold gold1]Current Version[/]").NoWrap());
        _ = table.AddColumn(new TableColumn("[bold]Status[/]").NoWrap());

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            _ = table.AddRow(
                new Markup($"[grey]{row.Index.Value}[/]"),
                new Markup(Markup.Escape(row.Component.Value)),
                new Markup(Markup.Escape(row.Repository.Value)),
                new Markup(Markup.Escape(row.CurrentVersion.Value)),
                new Markup(FormatStatus(row.Status)));
        }

        AnsiConsole.Write(table);
        RenderOutdatedVersionsSection(rows);
        RenderStatusDetailsSection(rows);
    }

    /// <inheritdoc />
    public void RenderSummary(IReadOnlyList<ComponentCheckRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var upToDate = rows.Count(x => x.Status == CheckStatus.UpToDate);
        var outdated = rows.Count(x => x.Status == CheckStatus.Outdated);
        var notFound = rows.Count(x => x.Status == CheckStatus.RepositoryNotFound);
        var errors = rows.Count(x => x.Status == CheckStatus.BitbucketError);
        var invalid = rows.Count(x => x.Status == CheckStatus.InvalidCurrentVersion);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[grey]Total:[/] [silver]{rows.Count}[/]");
        AnsiConsole.MarkupLine($"[grey]Up to date:[/] [green]{upToDate}[/]");
        AnsiConsole.MarkupLine($"[grey]Outdated:[/] [yellow]{outdated}[/]");

        if (notFound > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Repo not found:[/] [red]{notFound}[/]");
        }

        if (errors > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Bitbucket errors:[/] [red]{errors}[/]");
        }

        if (invalid > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Invalid current version:[/] [red]{invalid}[/]");
        }
    }

    /// <inheritdoc />
    public void RenderUniqueJiraTaskStatusChart(IReadOnlyList<ComponentCheckRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var uniqueTaskCountsByStatus = BuildUniqueJiraTaskCountsByStatus(rows);
        if (uniqueTaskCountsByStatus.Count == 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]No Jira tasks available for status chart.[/]");
            return;
        }

        var orderedEntries = uniqueTaskCountsByStatus
            .OrderByDescending(static x => x.Value)
            .ThenBy(static x => x.Key)
            .ToArray();

        var chart = new BarChart()
            .Width(80)
            .Label("[bold]Unique Jira Tasks By Status[/]")
            .CenterLabel();

        for (var i = 0; i < orderedEntries.Length; i++)
        {
            var (status, taskCount) = orderedEntries[i];
            _ = chart.AddItem(status.Value, taskCount, _statusChartColors[i % _statusChartColors.Length]);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(chart);
    }

    /// <inheritdoc />
    public void PrintError(ErrorMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Value))
        {
            throw new ArgumentException("Error message must not be empty.", nameof(message));
        }

        AnsiConsole.MarkupLine($"[bold red]Error:[/] {Markup.Escape(message.Value)}");
    }

    private static string FormatStatus(CheckStatus status)
    {
        return status switch
        {
            CheckStatus.UpToDate => "[green]Up to date[/]",
            CheckStatus.Outdated => "[yellow]Outdated[/]",
            CheckStatus.RepositoryNotFound => "[red]Repo not found[/]",
            CheckStatus.BitbucketError => "[red]Bitbucket error[/]",
            CheckStatus.InvalidCurrentVersion => "[red]Invalid version[/]",
            _ => "[grey]Unknown[/]",
        };
    }

    private static void RenderOutdatedVersionsSection(IReadOnlyList<ComponentCheckRow> rows)
    {
        var outdatedRows = rows
            .Where(static row => row.Status == CheckStatus.Outdated && row.NewerVersions.Count > 0)
            .ToArray();

        if (outdatedRows.Length == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]Outdated Components - Newer Versions[/]").RuleStyle("grey").LeftJustified());

        foreach (var row in outdatedRows)
        {
            AnsiConsole.MarkupLine(
                $"[bold yellow]{row.Index.Value}. {Markup.Escape(row.Component.Value)}[/] [grey]({Markup.Escape(row.Repository.Value)})[/] {FormatAheadCounterMarkup(row.NewerVersions.Count, row.CurrentVersion.Value)}");

            var versionsTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey37)
                .Expand();

            _ = versionsTable.AddColumn(new TableColumn("[grey]Version[/]").NoWrap());
            _ = versionsTable.AddColumn(new TableColumn("[grey]JiraTask[/]"));
            _ = versionsTable.AddColumn(new TableColumn("[grey]JiraTitle[/]"));
            _ = versionsTable.AddColumn(new TableColumn("[grey]JiraStatus[/]"));
            _ = versionsTable.AddColumn(new TableColumn("[grey]Alerts[/]").NoWrap());

            foreach (var version in row.NewerVersions)
            {
                _ = versionsTable.AddRow(
                    Markup.Escape(version.Version.Value),
                    Markup.Escape(version.JiraTask.Value),
                    Markup.Escape(version.JiraTitle.Value),
                    Markup.Escape(version.JiraStatus.Value),
                    FormatAlertMarkup(version.HasRequiredActions, version.HasBreakingChanges, version.HasDependencyIssues));
            }

            AnsiConsole.Write(versionsTable);
            RenderReleaseAlertDetails(row.NewerVersions);
        }
    }

    private static void RenderStatusDetailsSection(IReadOnlyList<ComponentCheckRow> rows)
    {
        var rowsWithDetails = rows
            .Where(static row => HasDetails(row.DetailsMessage.Value))
            .ToArray();

        if (rowsWithDetails.Length == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold]Status Details[/]").RuleStyle("grey").LeftJustified());

        var detailsTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Expand();

        _ = detailsTable.AddColumn(new TableColumn("[bold]#[/]").RightAligned());
        _ = detailsTable.AddColumn(new TableColumn("[bold]Component[/]"));
        _ = detailsTable.AddColumn(new TableColumn("[bold]Status[/]").NoWrap());
        _ = detailsTable.AddColumn(new TableColumn("[bold]Details[/]"));

        foreach (var row in rowsWithDetails)
        {
            _ = detailsTable.AddRow(
                new Markup($"[grey]{row.Index.Value}[/]"),
                new Markup(Markup.Escape(row.Component.Value)),
                new Markup(FormatStatus(row.Status)),
                new Markup(Markup.Escape(row.DetailsMessage.Value)));
        }

        AnsiConsole.Write(detailsTable);
    }

    private static bool HasDetails(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !string.Equals(value.Trim(), "-", StringComparison.Ordinal);
    }

    private static void RenderReleaseAlertDetails(IReadOnlyList<VersionJiraRow> versions)
    {
        var detailsByTask = BuildTaskAlertDetailsByTask(versions);

        var breakingChanges = detailsByTask
            .Where(static detail => HasDetails(detail.BreakingChangesDetails))
            .ToArray();
        var requiredActions = detailsByTask
            .Where(static detail => HasDetails(detail.RequiredActionsDetails))
            .ToArray();

        if (breakingChanges.Length == 0 && requiredActions.Length == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        RenderReleaseAlertSection(
            "Breaking Changes",
            breakingChanges,
            static detail => detail.BreakingChangesDetails ?? string.Empty);
        RenderReleaseAlertSection(
            "Required Actions",
            requiredActions,
            static detail => detail.RequiredActionsDetails ?? string.Empty);
    }

    private static void RenderReleaseAlertSection(
        string title,
        JiraTaskAlertDetails[] taskDetails,
        Func<JiraTaskAlertDetails, string> detailSelector)
    {
        if (taskDetails.Length == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(title)}[/]");

        foreach (var taskDetail in taskDetails)
        {
            AnsiConsole.MarkupLine(
                $"[silver]•[/] [bold]{Markup.Escape(taskDetail.Task.Value)}[/] [grey]{Markup.Escape(taskDetail.Title.Value)}[/]");

            var detailText = detailSelector(taskDetail);
            var detailPanel = new Panel(new Text(detailText))
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey37)
                .Expand();

            AnsiConsole.Write(detailPanel);
        }

        AnsiConsole.WriteLine();
    }

    private static JiraTaskAlertDetails[] BuildTaskAlertDetailsByTask(IReadOnlyList<VersionJiraRow> versions)
    {
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
        var requiredActionsDetails = HasDetails(current.RequiredActionsDetails)
            ? current.RequiredActionsDetails
            : candidate.RequiredActionsDetails;
        var breakingChangesDetails = HasDetails(current.BreakingChangesDetails)
            ? current.BreakingChangesDetails
            : candidate.BreakingChangesDetails;

        return new JiraTaskAlertDetails(
            current.Task,
            title,
            status,
            requiredActionsDetails,
            breakingChangesDetails);
    }

    private static string FormatAlertMarkup(bool hasRequiredActions, bool hasBreakingChanges, bool hasDependencyIssues)
    {
        if (!hasRequiredActions && !hasBreakingChanges && !hasDependencyIssues)
        {
            return "[grey]-[/]";
        }

        var renderedLabels = new List<string>(3);
        if (hasRequiredActions)
        {
            renderedLabels.Add("[bold orange1]RA[/]");
        }

        if (hasBreakingChanges)
        {
            renderedLabels.Add("[bold red]BC[/]");
        }

        if (hasDependencyIssues)
        {
            renderedLabels.Add("[bold deepskyblue2]D[/]");
        }

        return string.Join(" ", renderedLabels);
    }

    private static string FormatAheadCounterMarkup(int newerVersionCount, string currentVersion)
    {
        if (newerVersionCount <= 0)
        {
            return $"[grey]0 releases ahead (current: {Markup.Escape(currentVersion)})[/]";
        }

        var color = newerVersionCount switch
        {
            <= 2 => "yellow",
            <= 5 => "orange1",
            _ => "red",
        };

        var counterLabel = newerVersionCount == 1
            ? "1 release ahead"
            : string.Format(CultureInfo.InvariantCulture, "{0} releases ahead", newerVersionCount);

        return $"[bold {color}]{counterLabel}[/] [grey](current: {Markup.Escape(currentVersion)})[/]";
    }

    private static Dictionary<JiraStatusName, int> BuildUniqueJiraTaskCountsByStatus(
        IReadOnlyList<ComponentCheckRow> rows)
    {
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

    private readonly AppSettings _settings;
    private static readonly Color[] _statusChartColors =
    [
        Color.CornflowerBlue,
        Color.SpringGreen2,
        Color.Gold1,
        Color.Orange1,
        Color.DeepSkyBlue2,
        Color.MediumPurple
    ];
}
