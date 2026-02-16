using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;

namespace NReleaseBuilder.Presentation;

public sealed class SpectreConsoleRenderer
{
    public void RenderHeader(AppSettings settings)
    {
        AnsiConsole.Write(new Rule("[bold deepskyblue1]Components Version Check[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.MarkupLine($"[grey]Source:[/] [silver]{Markup.Escape(settings.CsvFilePath)}[/]");
        AnsiConsole.MarkupLine($"[grey]Workspace:[/] [silver]{Markup.Escape(settings.Bitbucket.Workspace)}[/]");
        if (settings.Jira.AllowedTaskStatuses.Count > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Jira Status Filter:[/] [silver]{Markup.Escape(string.Join(", ", settings.Jira.AllowedTaskStatuses))}[/]");
        }
        AnsiConsole.WriteLine();
    }

    public void PrintRepositoryCheckCount(int repositoryCount)
    {
        AnsiConsole.MarkupLine($"[grey]Checking Bitbucket tags for {repositoryCount} repositories...[/]");
    }

    public async Task<T> RunBitbucketLoadingWithProgressAsync<T>(
        IReadOnlyList<string> repositories,
        Func<BitbucketProgressCallbacks, Task<T>> operation)
    {
        T? result = default;
        Exception? error = null;

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
                    .Distinct(StringComparer.OrdinalIgnoreCase)
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

                try
                {
                    result = await operation(callbacks).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            }).ConfigureAwait(false);

        if (error is not null)
        {
            throw error;
        }

        return result!;
    }

    public void PrintNoRows()
    {
        AnsiConsole.MarkupLine("[yellow]No rows found in CSV.[/]");
    }

    public void PrintNoComponentsMatchedStatusFilter(IReadOnlyList<string> statuses)
    {
        var label = statuses is null || statuses.Count == 0
            ? "configured statuses"
            : string.Join(", ", statuses);
        AnsiConsole.MarkupLine($"[yellow]No components matched Jira status filter:[/] [grey]{Markup.Escape(label)}[/]");
    }

    public void PrintStatusFilterDiagnostics(
        IReadOnlyDictionary<string, int> statusStatistics,
        IReadOnlyList<string> allowedStatuses)
    {
        if (statusStatistics.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No Jira statuses were resolved for newer versions.[/]");
            return;
        }

        var allowed = new HashSet<string>(
            allowedStatuses.Where(static x => !string.IsNullOrWhiteSpace(x)).Select(static x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var topDisallowed = statusStatistics
            .Where(x => !allowed.Contains(x.Key))
            .OrderByDescending(static x => x.Value)
            .ThenBy(static x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(static x => $"{x.Key} ({x.Value})")
            .ToArray();

        if (topDisallowed.Length == 0)
        {
            AnsiConsole.MarkupLine("[grey]All collected statuses are allowed, but no component passed the all-tasks rule.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[grey]Top disallowed statuses:[/] {Markup.Escape(string.Join(", ", topDisallowed))}");
    }

    public void RenderTable(IReadOnlyList<ComponentCheckRow> rows)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Expand();

        table.AddColumn(new TableColumn("[bold]#[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold deepskyblue1]Component[/]"));
        table.AddColumn(new TableColumn("[bold springgreen2]Repository[/]"));
        table.AddColumn(new TableColumn("[bold gold1]Current[/]").NoWrap());
        table.AddColumn(new TableColumn("[bold]Status[/]").NoWrap());
        table.AddColumn(new TableColumn("[bold]Newer Versions[/]"));

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            table.AddRow(
                new Markup($"[grey]{i + 1}[/]"),
                new Markup(Markup.Escape(row.Component)),
                new Markup(Markup.Escape(row.Repository)),
                new Markup(Markup.Escape(row.CurrentVersion)),
                new Markup(FormatStatus(row.Status)),
                BuildNewerVersionsCell(row));
        }

        AnsiConsole.Write(table);
    }

    public void RenderSummary(IReadOnlyList<ComponentCheckRow> rows)
    {
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

    public void RenderSlackCopyText(IReadOnlyList<ComponentCheckRow> rows, IReadOnlyList<string> allowedStatuses)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Slack copy:");
        builder.AppendLine($"Jira filter: {string.Join(", ", allowedStatuses)}");
        builder.AppendLine();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            builder.AppendLine($"{i + 1}. {row.Component} | {row.Repository} | current {row.CurrentVersion}");

            if (row.NewerVersions.Count == 0)
            {
                builder.AppendLine("   - no newer versions");
                continue;
            }

            foreach (var version in row.NewerVersions)
            {
                var jiraTask = string.IsNullOrWhiteSpace(version.JiraTask) ? "N/A" : version.JiraTask;
                var jiraStatus = string.IsNullOrWhiteSpace(version.JiraStatus) ? "N/A" : version.JiraStatus;
                builder.AppendLine($"   - {version.Version} | {jiraTask} | {jiraStatus}");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Slack-ready summary");
        AnsiConsole.WriteLine(builder.ToString().TrimEnd());
    }

    public void PrintError(string message)
    {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] {Markup.Escape(message)}");
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

        subTable.AddColumn(new TableColumn("[grey]Version[/]").NoWrap());
        subTable.AddColumn(new TableColumn("[grey]JiraTask[/]"));
        subTable.AddColumn(new TableColumn("[grey]JiraStatus[/]"));

        foreach (var item in row.NewerVersions)
        {
            var jiraTask = string.IsNullOrWhiteSpace(item.JiraTask) ? "N/A" : item.JiraTask;
            var jiraStatus = string.IsNullOrWhiteSpace(item.JiraStatus) ? "N/A" : item.JiraStatus;
            subTable.AddRow(Markup.Escape(item.Version), Markup.Escape(jiraTask), Markup.Escape(jiraStatus));
        }

        return subTable;
    }
}
