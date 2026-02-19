using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

using QuestPDF.Fluent;

namespace NReleaseBuilder.Presentation.Pdf;

/// <summary>
/// Default implementation for composing PDF page content.
/// </summary>
public sealed partial class PdfContentComposer : IPdfContentComposer
{
    private const string JIRA_LINK_COLOR_HEX = "#1d4ed8";

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfContentComposer"/> class.
    /// </summary>
    public PdfContentComposer()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfContentComposer"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    public PdfContentComposer(IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _jiraBrowseBaseUrl = BuildBaseJiraUrl(options.Value.Jira.BaseUrl);
    }

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

    private void ComposePdfResultsSection(ColumnDescriptor column, IReadOnlyList<ComponentCheckRow> rows)
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

    private void ComposePdfOutdatedVersionsSection(
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
                    .Bold()
                    .FontSize(11);

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
                        table.Cell().Element(PdfPresentationHelpers.StylePdfBodyCell).Text(text =>
                            ComposeJiraTaskText(text, version.JiraTask.Value, isBold: false));
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

    private void ComposePdfReleaseAlertDetailsSection(
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

    private void ComposePdfReleaseAlertGroup(
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
            column.Item().Text(text =>
            {
                ComposeJiraTaskText(text, taskDetail.Task.Value, isBold: true);
                _ = text.Span(" - " + taskDetail.Title.Value).Bold();
            });
            column
                .Item()
                .Element(PdfPresentationHelpers.StylePdfAlertDetailsBox)
                .Text(text => ComposeJiraAwareDetailsText(text, detailSelector(taskDetail)));
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

    private void ComposeJiraTaskText(TextDescriptor text, string taskReference, bool isBold)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(taskReference);

        var taskValues = SplitTaskValues(taskReference);
        if (taskValues.Length == 0)
        {
            var fallbackSpan = text.Span(taskReference);
            if (isBold)
            {
                _ = fallbackSpan.Bold();
            }

            return;
        }

        for (var index = 0; index < taskValues.Length; index++)
        {
            if (index > 0)
            {
                _ = text.Span(", ");
            }

            var taskValue = taskValues[index];
            if (TryBuildJiraBrowseTaskUrl(taskValue, out var taskUrl))
            {
                var hyperlink = text.Hyperlink(taskValue, taskUrl);
                _ = ApplyJiraHyperlinkStyle(hyperlink);

                if (isBold)
                {
                    _ = hyperlink.Bold();
                }

                continue;
            }

            var plainSpan = text.Span(taskValue);
            if (isBold)
            {
                _ = plainSpan.Bold();
            }
        }
    }

    private void ComposeJiraAwareDetailsText(TextDescriptor text, string details)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(details);

        var currentIndex = 0;

        foreach (Match browseUrlMatch in JiraBrowseUrlRegex().Matches(details))
        {
            if (browseUrlMatch.Index > currentIndex)
            {
                var segment = details[currentIndex..browseUrlMatch.Index];
                AppendSegmentWithTaskLinks(text, segment);
            }

            var browseUrl = browseUrlMatch.Value;
            var browseUrlHyperlink = text.Hyperlink(browseUrl, browseUrl);
            _ = ApplyJiraHyperlinkStyle(browseUrlHyperlink);
            currentIndex = browseUrlMatch.Index + browseUrlMatch.Length;
        }

        if (currentIndex < details.Length)
        {
            var tailSegment = details[currentIndex..];
            AppendSegmentWithTaskLinks(text, tailSegment);
        }
    }

    private void AppendSegmentWithTaskLinks(TextDescriptor text, string segment)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(segment);

        if (segment.Length == 0)
        {
            return;
        }

        var currentIndex = 0;

        foreach (Match taskMatch in JiraTaskKeyRegex().Matches(segment))
        {
            if (taskMatch.Index > currentIndex)
            {
                _ = text.Span(segment[currentIndex..taskMatch.Index]);
            }

            var taskValue = taskMatch.Groups["task"].Value;
            if (TryBuildJiraBrowseTaskUrl(taskValue, out var taskUrl))
            {
                var taskHyperlink = text.Hyperlink(taskValue, taskUrl);
                _ = ApplyJiraHyperlinkStyle(taskHyperlink);
            }
            else
            {
                _ = text.Span(taskValue);
            }

            currentIndex = taskMatch.Index + taskMatch.Length;
        }

        if (currentIndex < segment.Length)
        {
            _ = text.Span(segment[currentIndex..]);
        }
    }

    private static TextSpanDescriptor ApplyJiraHyperlinkStyle(TextSpanDescriptor hyperlink) =>
        hyperlink
            .FontColor(JIRA_LINK_COLOR_HEX)
            .Underline();

    [GeneratedRegex(
        @"https?://[^\s\)\]\}<>""']+/browse/(?<task>[A-Za-z][A-Za-z0-9_]*-\d+)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex JiraBrowseUrlRegex();

    [GeneratedRegex(
        @"(?<![A-Za-z0-9_])(?<task>[A-Za-z][A-Za-z0-9_]*-\d+)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant)]
    private static partial Regex JiraTaskKeyRegex();

    private bool TryBuildJiraBrowseTaskUrl(string taskValue, out string taskUrl)
    {
        if (_jiraBrowseBaseUrl is null || !IsTrackableJiraTask(taskValue))
        {
            taskUrl = string.Empty;
            return false;
        }

        taskUrl = new Uri(_jiraBrowseBaseUrl, "browse/" + taskValue).ToString();
        return true;
    }

    private static Uri BuildBaseJiraUrl(Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        return new Uri(baseUrl.ToString().TrimEnd('/') + "/", UriKind.Absolute);
    }

    private static string[] SplitTaskValues(string value) =>
    [
        .. value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
    ];

    private static bool IsTrackableJiraTask(string taskKey)
    {
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

    private readonly Uri? _jiraBrowseBaseUrl;
}
