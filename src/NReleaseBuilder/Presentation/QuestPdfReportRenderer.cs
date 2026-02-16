using System.Globalization;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;

using QuestPDF.Fluent;
using QuestPDF.Helpers;

using QContainer = QuestPDF.Infrastructure.IContainer;
using QLicenseType = QuestPDF.Infrastructure.LicenseType;

namespace NReleaseBuilder.Presentation;

/// <summary>
/// QuestPDF implementation for PDF report rendering.
/// </summary>
public sealed class QuestPdfReportRenderer : IPdfReportRenderer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuestPdfReportRenderer"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    public QuestPdfReportRenderer(IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _settings = options.Value;
    }

    /// <inheritdoc />
    public void RenderReport(
        ComponentCheckRow[] rows,
        JiraStatusName[] allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(allowedStatuses);
        ArgumentNullException.ThrowIfNull(statusStatistics);

        if (!_settings.Pdf.Enabled)
        {
            return;
        }

        var outputPath = _settings.Pdf.ResolveOutputPath();
        var outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            _ = Directory.CreateDirectory(outputDirectory);
        }

        QuestPDF.Settings.License = QLicenseType.Community;

        Document
            .Create(container =>
            {
                _ = container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(static style => style.FontSize(9));

                    page.Header().Column(column =>
                    {
                        column.Spacing(2);
                        _ = column.Item().Text("Components Version Check").Bold().FontSize(16);
                        _ = column.Item().Text(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Generated: {0:yyyy-MM-dd HH:mm:ss zzz}",
                                DateTimeOffset.Now));
                        _ = column.Item().Text("Source: " + _settings.CsvFilePath);
                        _ = column.Item().Text("Workspace: " + _settings.Bitbucket.Workspace);

                        if (allowedStatuses.Length > 0)
                        {
                            _ = column.Item().Text(
                                "Jira Status Filter: "
                                + string.Join(", ", allowedStatuses.Select(static x => x.Value)));
                        }
                    });

                    page.Content().PaddingTop(8).Column(column =>
                    {
                        column.Spacing(10);

                        if (rows.Length == 0)
                        {
                            ComposePdfEmptyStateSection(column, allowedStatuses, statusStatistics);
                            return;
                        }

                        ComposePdfResultsSection(column, rows);
                        ComposePdfUniqueJiraTaskStatusSection(column, rows);
                    });

                    page.Footer().AlignRight().Text(text =>
                    {
                        _ = text.Span("Page ");
                        _ = text.CurrentPageNumber();
                        _ = text.Span(" / ");
                        _ = text.TotalPages();
                    });
                });
            })
            .GeneratePdf(outputPath);
    }

    private static void ComposePdfEmptyStateSection(
        ColumnDescriptor column,
        JiraStatusName[] allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics)
    {
        var label = allowedStatuses.Length == 0
            ? "configured statuses"
            : string.Join(", ", allowedStatuses.Select(static x => x.Value));

        _ = column
            .Item()
            .Text("No components matched Jira status filter: " + label)
            .Bold()
            .FontColor("#b45309");

        if (statusStatistics.Count == 0)
        {
            _ = column.Item().Text("No Jira statuses were resolved for newer versions.");
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
            _ = column.Item().Text("All collected statuses are allowed, but no component passed the all-tasks rule.");
            return;
        }

        _ = column.Item().Text("Top disallowed statuses: " + string.Join(", ", topDisallowed));
    }

    private static void ComposePdfResultsSection(ColumnDescriptor column, IReadOnlyList<ComponentCheckRow> rows)
    {
        _ = column.Item().Text("Results").Bold().FontSize(12);

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24);
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(2.4f);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(4.8f);
            });

            table.Header(header =>
            {
                _ = header.Cell().Element(StylePdfHeaderCell).Text("#");
                _ = header.Cell().Element(StylePdfHeaderCell).Text("Component");
                _ = header.Cell().Element(StylePdfHeaderCell).Text("Repository");
                _ = header.Cell().Element(StylePdfHeaderCell).Text("Current");
                _ = header.Cell().Element(StylePdfHeaderCell).Text("Status");
                _ = header.Cell().Element(StylePdfHeaderCell).Text("Newer Versions");
            });

            foreach (var row in rows)
            {
                _ = table.Cell().Element(StylePdfBodyCell).Text(row.Index.Value.ToString(CultureInfo.InvariantCulture));
                _ = table.Cell().Element(StylePdfBodyCell).Text(row.Component.Value);
                _ = table.Cell().Element(StylePdfBodyCell).Text(row.Repository.Value);
                _ = table.Cell().Element(StylePdfBodyCell).Text(row.CurrentVersion.Value);
                _ = table.Cell().Element(StylePdfBodyCell).Text(FormatStatusPlain(row.Status));
                table.Cell().Element(StylePdfBodyCell).Column(details =>
                {
                    _ = details
                        .Item()
                        .Text(BuildAheadCounterLabel(row.NewerVersions.Count))
                        .Bold()
                        .FontColor(ResolveAheadCounterHexColor(row.NewerVersions.Count));

                    if (row.NewerVersions.Count == 0)
                    {
                        _ = details.Item().Text(row.DetailsMessage.Value);
                        return;
                    }

                    _ = details.Item().Text("Version | JiraTask | JiraStatus | Alerts").FontColor("#6b7280");

                    foreach (var version in row.NewerVersions)
                    {
                        details.Item().Text(text =>
                        {
                            _ = text.Span(
                                version.Version.Value
                                + " | "
                                + version.JiraTask.Value
                                + " | "
                                + version.JiraStatus.Value
                                + " | ");

                            if (!version.HasRequiredActions && !version.HasBreakingChanges)
                            {
                                _ = text.Span("-").FontColor("#6b7280");
                                return;
                            }

                            if (version.HasRequiredActions)
                            {
                                _ = text.Span("RA").Bold().FontColor("#ffaf00");
                            }

                            if (version.HasBreakingChanges)
                            {
                                if (version.HasRequiredActions)
                                {
                                    _ = text.Span(" ");
                                }

                                _ = text.Span("BC").Bold().FontColor("#ff0000");
                            }
                        });
                    }
                });
            }
        });
    }

    private static void ComposePdfUniqueJiraTaskStatusSection(
        ColumnDescriptor column,
        IReadOnlyList<ComponentCheckRow> rows)
    {
        var uniqueTaskCountsByStatus = BuildUniqueJiraTaskCountsByStatus(rows);

        _ = column.Item().Text("Unique Jira Tasks By Status").Bold().FontSize(12);

        if (uniqueTaskCountsByStatus.Count == 0)
        {
            _ = column.Item().Text("No Jira tasks available for status chart.");
            return;
        }

        var orderedEntries = uniqueTaskCountsByStatus
            .OrderByDescending(static x => x.Value)
            .ThenBy(static x => x.Key)
            .ToArray();

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.ConstantColumn(110);
            });

            table.Header(header =>
            {
                _ = header.Cell().Element(StylePdfHeaderCell).Text("Status");
                _ = header.Cell().Element(StylePdfHeaderCell).AlignRight().Text("Unique Tasks");
            });

            foreach (var (status, taskCount) in orderedEntries)
            {
                _ = table.Cell().Element(StylePdfBodyCell).Text(status.Value);
                _ = table.Cell()
                    .Element(StylePdfBodyCell)
                    .AlignRight()
                    .Text(taskCount.ToString(CultureInfo.InvariantCulture));
            }
        });
    }

    private static QContainer StylePdfHeaderCell(QContainer container) =>
        container
            .Background("#f3f4f6")
            .Border(1)
            .BorderColor("#d1d5db")
            .PaddingHorizontal(6)
            .PaddingVertical(4);

    private static QContainer StylePdfBodyCell(QContainer container) =>
        container
            .BorderBottom(1)
            .BorderColor("#e5e7eb")
            .PaddingHorizontal(6)
            .PaddingVertical(4);

    private static string FormatStatusPlain(CheckStatus status)
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

    private static string BuildAheadCounterLabel(int newerVersionCount)
    {
        return newerVersionCount == 1
            ? "1 release ahead"
            : string.Format(CultureInfo.InvariantCulture, "{0} releases ahead", newerVersionCount);
    }

    private static string ResolveAheadCounterHexColor(int newerVersionCount)
    {
        return newerVersionCount switch
        {
            <= 0 => "#6b7280",
            <= 2 => "#ca8a04",
            <= 5 => "#ea580c",
            _ => "#b91c1c",
        };
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
}
