using System.Globalization;
using System.Text;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;

using Spectre.Console;
using Spectre.Console.Rendering;

namespace NReleaseBuilder.Presentation;

/// <summary>
/// Spectre.Console renderer for application output.
/// </summary>
public sealed class SpectreConsoleRenderer : IConsoleRenderer
{
    /// <inheritdoc />
    public void RenderHeader(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        AnsiConsole.Write(new Rule("[bold deepskyblue1]Components Version Check[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.MarkupLine($"[grey]Source:[/] [silver]{Markup.Escape(settings.CsvFilePath)}[/]");
        AnsiConsole.MarkupLine($"[grey]Workspace:[/] [silver]{Markup.Escape(settings.Bitbucket.Workspace)}[/]");
        if (settings.Jira.AllowedTaskStatuses.Count > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Jira Status Filter:[/] [silver]{Markup.Escape(string.Join(", ", settings.Jira.AllowedTaskStatuses))}[/]");
        }
        AnsiConsole.WriteLine();
    }

    /// <inheritdoc />
    public void PrintRepositoryCheckCount(int repositoryCount)
    {
        AnsiConsole.MarkupLine($"[grey]Checking Bitbucket tags for {repositoryCount} repositories...[/]");
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
    public void PrintNoRows()
    {
        AnsiConsole.MarkupLine("[yellow]No rows found in CSV.[/]");
    }

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
        _ = table.AddColumn(new TableColumn("[bold gold1]Current[/]").NoWrap());
        _ = table.AddColumn(new TableColumn("[bold]Status[/]").NoWrap());
        _ = table.AddColumn(new TableColumn("[bold]Newer Versions[/]"));

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            _ = table.AddRow(
                new Markup($"[grey]{i + 1}[/]"),
                new Markup(Markup.Escape(row.Component)),
                new Markup(Markup.Escape(row.Repository)),
                new Markup(Markup.Escape(row.CurrentVersion)),
                new Markup(FormatStatus(row.Status)),
                BuildNewerVersionsCell(row));
        }

        AnsiConsole.Write(table);
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
    public void RenderSlackCopyText(
        IReadOnlyList<ComponentCheckRow> rows,
        IReadOnlyList<JiraStatusName> allowedStatuses)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(allowedStatuses);

        if (rows.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        _ = builder.AppendLine("Slack copy:");
        _ = builder.AppendLine("Jira filter: " + string.Join(", ", allowedStatuses.Select(static x => x.Value)));
        _ = builder.AppendLine();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            _ = builder.AppendLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}. {1} | {2} | current {3}",
                    i + 1,
                    row.Component,
                    row.Repository,
                    row.CurrentVersion));

            if (row.NewerVersions.Count == 0)
            {
                _ = builder.AppendLine("   - no newer versions");
                continue;
            }

            foreach (var version in row.NewerVersions)
            {
                var jiraTask = string.IsNullOrWhiteSpace(version.JiraTask) ? "N/A" : version.JiraTask;
                var jiraStatus = string.IsNullOrWhiteSpace(version.JiraStatus) ? "N/A" : version.JiraStatus;
                _ = builder.AppendLine(
                    "   - " + version.Version + " | " + jiraTask + " | " + jiraStatus);
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Slack-ready summary");
        AnsiConsole.WriteLine(builder.ToString().TrimEnd());
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

    private static IRenderable BuildNewerVersionsCell(ComponentCheckRow row)
    {
        if (row.NewerVersions.Count == 0)
        {
            return new Markup(Markup.Escape(row.DetailsMessage));
        }

        var subTable = new Table()
            .Border(TableBorder.Minimal)
            .BorderColor(Color.Grey37)
            .Expand();

        _ = subTable.AddColumn(new TableColumn("[grey]Version[/]").NoWrap());
        _ = subTable.AddColumn(new TableColumn("[grey]JiraTask[/]"));
        _ = subTable.AddColumn(new TableColumn("[grey]JiraStatus[/]"));

        foreach (var item in row.NewerVersions)
        {
            var jiraTask = string.IsNullOrWhiteSpace(item.JiraTask) ? "N/A" : item.JiraTask;
            var jiraStatus = string.IsNullOrWhiteSpace(item.JiraStatus) ? "N/A" : item.JiraStatus;
            _ = subTable.AddRow(Markup.Escape(item.Version), Markup.Escape(jiraTask), Markup.Escape(jiraStatus));
        }

        return subTable;
    }
}
