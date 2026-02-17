using System.Globalization;

using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Models;

using QuestPDF.Fluent;

namespace NReleaseBuilder.Presentation.Pdf;

/// <summary>
/// Default implementation for composing PDF page content.
/// </summary>
public sealed class PdfContentComposer : IPdfContentComposer
{
    /// <inheritdoc />
    public void ComposeContent(
        ColumnDescriptor column,
        ComponentCheckRow[] rows,
        JiraStatusName[] allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(allowedStatuses);
        ArgumentNullException.ThrowIfNull(statusStatistics);

        column.Spacing(10);

        if (rows.Length == 0)
        {
            ComposePdfEmptyStateSection(column, allowedStatuses, statusStatistics);
            return;
        }

        ComposePdfResultsSection(column, rows);
        ComposePdfUniqueJiraTaskStatusSection(column, rows);
    }

    private static void ComposePdfEmptyStateSection(
        ColumnDescriptor column,
        JiraStatusName[] allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics)
    {
        var label = allowedStatuses.BuildStatusFilterLabel();

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

        var topDisallowed = statusStatistics.BuildTopDisallowedStatusLabels(allowedStatuses);

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
                columns.RelativeColumn(1.6f);
                columns.RelativeColumn(1.4f);
            });

            table.Header(header =>
            {
                _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("#");
                _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("Component");
                _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("Repository");
                _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("Current Version");
                _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("Status");
            });

            foreach (var row in rows)
            {
                _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(row.Index.Value.ToString(CultureInfo.InvariantCulture));
                _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(row.Component.Value);
                _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(row.Repository.Value);
                _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(row.CurrentVersion.Value);
                _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(row.Status.ToPlainLabel());
            }
        });

        ComposePdfOutdatedVersionsSection(column, rows);
        ComposePdfStatusDetailsSection(column, rows);
    }

    private static void ComposePdfOutdatedVersionsSection(
        ColumnDescriptor column,
        IReadOnlyList<ComponentCheckRow> rows)
    {
        var outdatedRows = rows
            .Where(static row => row.Status == CheckStatus.Outdated && row.NewerVersions.Count > 0)
            .ToArray();

        if (outdatedRows.Length == 0)
        {
            return;
        }

        _ = column.Item().Text("Outdated Components - Newer Versions").Bold().FontSize(11);

        foreach (var row in outdatedRows)
        {
            column.Item().Column(details =>
            {
                details.Spacing(2);

                _ = details
                    .Item()
                    .PaddingTop(4)
                    .Text(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}. {1} ({2})",
                            row.Index.Value,
                            row.Component.Value,
                            row.Repository.Value))
                    .Bold();

                _ = details
                    .Item()
                    .Text(row.NewerVersions.Count.ToAheadReleasesLabel() + " (current: " + row.CurrentVersion.Value + ")")
                    .Bold()
                    .FontColor(PdfPresentationHelpers.ResolveAheadCounterHexColor(row.NewerVersions.Count));

                details.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.8f);
                        columns.RelativeColumn(3.0f);
                        columns.RelativeColumn(2.0f);
                        columns.ConstantColumn(55);
                    });

                    table.Header(header =>
                    {
                        _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("Version");
                        _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("JiraTask");
                        _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("JiraTitle");
                        _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("JiraStatus");
                        _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("Alerts");
                    });

                    foreach (var version in row.NewerVersions)
                    {
                        _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(version.Version.Value);
                        _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(version.JiraTask.Value);
                        _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(version.JiraTitle.Value);
                        _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(version.JiraStatus.Value);
                        table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(text =>
                        {
                            if (!version.HasRequiredActions && !version.HasBreakingChanges && !version.HasDependencyIssues)
                            {
                                _ = text.Span("-").FontColor("#6b7280");
                                return;
                            }

                            var hasAnyLabel = false;

                            if (version.HasRequiredActions)
                            {
                                _ = text.Span("RA").Bold().FontColor("#ffaf00");
                                hasAnyLabel = true;
                            }

                            if (version.HasBreakingChanges)
                            {
                                if (hasAnyLabel)
                                {
                                    _ = text.Span(" ");
                                }

                                _ = text.Span("BC").Bold().FontColor("#ff0000");
                                hasAnyLabel = true;
                            }

                            if (version.HasDependencyIssues)
                            {
                                if (hasAnyLabel)
                                {
                                    _ = text.Span(" ");
                                }

                                _ = text.Span("D").Bold().FontColor("#00afff");
                            }
                        });
                    }
                });

                ComposePdfReleaseAlertDetailsSection(details, row.NewerVersions);
            });
        }
    }

    private static void ComposePdfReleaseAlertDetailsSection(
        ColumnDescriptor column,
        IReadOnlyList<VersionJiraRow> versions)
    {
        var detailsByTask = versions.BuildTaskAlertDetailsByTask();

        var breakingChanges = detailsByTask
            .Where(static detail => detail.BreakingChangesDetails.HasDetails())
            .ToArray();
        var requiredActions = detailsByTask
            .Where(static detail => detail.RequiredActionsDetails.HasDetails())
            .ToArray();

        if (breakingChanges.Length == 0 && requiredActions.Length == 0)
        {
            return;
        }

        ComposePdfReleaseAlertGroup(
            column,
            "Breaking Changes",
            breakingChanges,
            static detail => detail.BreakingChangesDetails ?? string.Empty);
        ComposePdfReleaseAlertGroup(
            column,
            "Required Actions",
            requiredActions,
            static detail => detail.RequiredActionsDetails ?? string.Empty);
    }

    private static void ComposePdfReleaseAlertGroup(
        ColumnDescriptor column,
        string title,
        JiraTaskAlertDetails[] taskDetails,
        Func<JiraTaskAlertDetails, string> detailSelector)
    {
        if (taskDetails.Length == 0)
        {
            return;
        }

        _ = column.Item().PaddingTop(6).Text(title).Bold();

        foreach (var taskDetail in taskDetails)
        {
            _ = column.Item().Text(taskDetail.Task.Value + " - " + taskDetail.Title.Value).Bold();
            _ = column.Item().Element(PdfPresentationHelpers.StylePdfAlertDetailsBox).Text(detailSelector(taskDetail));
        }
    }

    private static void ComposePdfStatusDetailsSection(
        ColumnDescriptor column,
        IReadOnlyList<ComponentCheckRow> rows)
    {
        var rowsWithDetails = rows
            .Where(static row => row.DetailsMessage.Value.HasDetails())
            .ToArray();

        if (rowsWithDetails.Length == 0)
        {
            return;
        }

        _ = column.Item().Text("Status Details").Bold().FontSize(11);

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24);
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(4.4f);
            });

            table.Header(header =>
            {
                _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("#");
                _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("Component");
                _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("Status");
                _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("Details");
            });

            foreach (var row in rowsWithDetails)
            {
                _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(row.Index.Value.ToString(CultureInfo.InvariantCulture));
                _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(row.Component.Value);
                _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(row.Status.ToPlainLabel());
                _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(row.DetailsMessage.Value);
            }
        });
    }

    private static void ComposePdfUniqueJiraTaskStatusSection(
        ColumnDescriptor column,
        IReadOnlyList<ComponentCheckRow> rows)
    {
        var uniqueTaskCountsByStatus = rows.BuildUniqueJiraTaskCountsByStatus();

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
                _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).Text("Status");
                _ = header.Cell().Element(PdfPresentationHelpers.StylePdfHeaderCell).AlignRight().Text("Unique Tasks");
            });

            foreach (var (status, taskCount) in orderedEntries)
            {
                _ = table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(status.Value);
                _ = table.Cell()
                    .Element(PdfPresentationHelpers.StylePdfBodyCell)
                    .AlignRight()
                    .Text(taskCount.ToString(CultureInfo.InvariantCulture));
            }
        });
    }
}
