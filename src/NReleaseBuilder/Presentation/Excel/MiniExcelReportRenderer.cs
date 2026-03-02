using System.Globalization;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using Microsoft.Extensions.Options;

using MiniExcelLibs;

using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Presentation.Pdf;

namespace NReleaseBuilder.Presentation.Excel;

/// <summary>
/// MiniExcel-based implementation for Excel report rendering.
/// </summary>
public sealed partial class MiniExcelReportRenderer : IExcelReportRenderer
{
    private const int SUMMARY_COLUMN_COUNT = 8;
    private const int COMPONENT_COLUMN_COUNT = 5;
    private const string SUMMARY_SHEET_NAME = "Summary";
    private const string LINK_COLOR_HEX = "1D4ED8";
    private const string HEADER_FILL_HEX = "F3F4F6";
    private const string ALERT_DETAILS_FILL_HEX = "F9FAFB";
    private const string BORDER_HEX = "D1D5DB";
    private const string MUTED_HEX = "6B7280";

    /// <summary>
    /// Initializes a new instance of the <see cref="MiniExcelReportRenderer"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    public MiniExcelReportRenderer(IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _settings = options.Value;
        _jiraBrowseBaseUrl = BuildBaseJiraUrl(_settings.Jira.BaseUrl);
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

        if (!_settings.Excel.Enabled)
        {
            return;
        }

        var workbook = BuildWorkbook(rows, allowedStatuses, statusStatistics);
        var outputPath = _settings.Excel.ResolveOutputPath();
        var outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            _ = Directory.CreateDirectory(outputDirectory);
        }

        _ = MiniExcel.SaveAs(outputPath, workbook.Sheets, printHeader: false, overwriteFile: true);
        ApplyWorkbookFormatting(outputPath, workbook.Layouts);

        System.Console.WriteLine($"Excel report saved to: {outputPath}");
    }

    private WorkbookData BuildWorkbook(
        IReadOnlyList<ComponentCheckRow> rows,
        IReadOnlyList<JiraStatusName> allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics)
    {
        var sheets = new Dictionary<string, object>(StringComparer.Ordinal);
        var layouts = new Dictionary<string, SheetLayout>(StringComparer.Ordinal);

        var summarySheet = BuildSummarySheet(rows, allowedStatuses, statusStatistics);
        sheets.Add(summarySheet.Name, summarySheet.Rows);
        layouts.Add(summarySheet.Name, summarySheet.Layout);

        foreach (var componentSheet in BuildComponentSheets(rows))
        {
            sheets.Add(componentSheet.Name, componentSheet.Rows);
            layouts.Add(componentSheet.Name, componentSheet.Layout);
        }

        return new WorkbookData(sheets, layouts);
    }

    private BuiltSheet BuildSummarySheet(
        IReadOnlyList<ComponentCheckRow> rows,
        IReadOnlyList<JiraStatusName> allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics)
    {
        var values = new List<Dictionary<string, object?>>();
        var layout = new SheetLayout(SUMMARY_SHEET_NAME)
        {
            ColumnWidths =
            {
                [1] = 14,
                [2] = 34,
                [3] = 30,
                [4] = 18,
                [5] = 18,
                [6] = 18,
                [7] = 28,
                [8] = 14,
            },
        };

        AddRow(values, layout, ExcelCellStyleKind.Title, "A", "Components Version Check", SUMMARY_COLUMN_COUNT);
        AddLabeledValueRow(values, layout, "Generated", DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture), SUMMARY_COLUMN_COUNT);
        AddLabeledValueRow(values, layout, "Source", _settings.CsvFilePath, SUMMARY_COLUMN_COUNT);
        AddLabeledValueRow(values, layout, "Workspace", _settings.Bitbucket.Workspace, SUMMARY_COLUMN_COUNT);
        AddLabeledValueRow(values, layout, "Jira Status Filter", allowedStatuses.BuildStatusFilterLabel(), SUMMARY_COLUMN_COUNT);
        AddBlankRow(values, SUMMARY_COLUMN_COUNT);

        if (rows.Count == 0)
        {
            AddRow(values, layout, ExcelCellStyleKind.SectionTitle, "A", "Results", SUMMARY_COLUMN_COUNT);
            AddRow(values, layout, ExcelCellStyleKind.Muted, "A", "No components matched the configured Jira status filter.", SUMMARY_COLUMN_COUNT);

            if (statusStatistics.Count == 0)
            {
                AddRow(values, layout, ExcelCellStyleKind.Muted, "A", "No Jira statuses were resolved for newer versions.", SUMMARY_COLUMN_COUNT);
            }
            else
            {
                var topDisallowed = statusStatistics.BuildTopDisallowedStatusLabels(allowedStatuses);
                var diagnostics = topDisallowed.Length == 0
                    ? "All collected statuses are allowed, but no component passed the all-tasks rule."
                    : "Top disallowed statuses: " + string.Join(", ", topDisallowed);

                AddRow(values, layout, ExcelCellStyleKind.Muted, "A", diagnostics, SUMMARY_COLUMN_COUNT);
            }

            return new BuiltSheet(SUMMARY_SHEET_NAME, values, layout);
        }

        AddRow(values, layout, ExcelCellStyleKind.SectionTitle, "A", "Results", SUMMARY_COLUMN_COUNT);
        var resultsHeaderRow = values.Count + 1;
        values.Add(CreateGridRow(
            SUMMARY_COLUMN_COUNT,
            "#",
            "Component",
            "Repository",
            "Current Version",
            "Status"));
        layout.TableRanges.Add(new TableRange(resultsHeaderRow, 1, 5, resultsHeaderRow + 1, resultsHeaderRow + rows.Count));

        foreach (var row in rows)
        {
            var statusCellReference = ToCellReference(5, values.Count + 1);
            values.Add(CreateGridRow(
                SUMMARY_COLUMN_COUNT,
                row.Index.Value.ToString(CultureInfo.InvariantCulture),
                row.Component.Value,
                row.Repository.Value,
                row.CurrentVersion.Value,
                row.Status.ToPlainLabel()));
            layout.CellStyles[statusCellReference] = ResolveStatusStyle(row.Status, row.NewerVersions.Count);
        }

        AddBlankRow(values, SUMMARY_COLUMN_COUNT);
        AddRow(values, layout, ExcelCellStyleKind.SectionTitle, "A", "Unique Jira Tasks By Status", SUMMARY_COLUMN_COUNT);

        var uniqueTaskCountsByStatus = rows.BuildUniqueJiraTaskCountsByStatus();
        if (uniqueTaskCountsByStatus.Count == 0)
        {
            AddRow(values, layout, ExcelCellStyleKind.Muted, "A", "No Jira tasks available for status chart.", SUMMARY_COLUMN_COUNT);
            return new BuiltSheet(SUMMARY_SHEET_NAME, values, layout);
        }

        var chartHeaderRow = values.Count + 1;
        values.Add(CreateGridRow(
            SUMMARY_COLUMN_COUNT,
            "Status",
            "Unique Tasks"));

        var orderedEntries = uniqueTaskCountsByStatus
            .OrderByDescending(static x => x.Value)
            .ThenBy(static x => x.Key)
            .ToArray();

        layout.TableRanges.Add(new TableRange(chartHeaderRow, 1, 2, chartHeaderRow + 1, chartHeaderRow + orderedEntries.Length));

        foreach (var (status, taskCount) in orderedEntries)
        {
            values.Add(CreateGridRow(
                SUMMARY_COLUMN_COUNT,
                status.Value,
                taskCount.ToString(CultureInfo.InvariantCulture)));
        }

        return new BuiltSheet(SUMMARY_SHEET_NAME, values, layout);
    }

    private IEnumerable<BuiltSheet> BuildComponentSheets(IReadOnlyList<ComponentCheckRow> rows)
    {
        var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SUMMARY_SHEET_NAME,
        };

        foreach (var row in rows)
        {
            var values = new List<Dictionary<string, object?>>();
            var layout = new SheetLayout(string.Empty)
            {
                ColumnWidths =
                {
                    [1] = 18,
                    [2] = 24,
                    [3] = 46,
                    [4] = 20,
                    [5] = 14,
                },
            };

            var baseName = $"{row.Index.Value:D2}_{row.Component.Value}";
            var sheetName = BuildUniqueSheetName(baseName, usedSheetNames);
            layout.Name = sheetName;

            AddRow(values, layout, ExcelCellStyleKind.Title, "A", $"{row.Index.Value}. {row.Component.Value}", COMPONENT_COLUMN_COUNT);
            AddLabeledValueRow(values, layout, "Repository", row.Repository.Value, COMPONENT_COLUMN_COUNT);
            AddLabeledValueRow(values, layout, "Current Version", row.CurrentVersion.Value, COMPONENT_COLUMN_COUNT);
            AddLabeledValueRow(values, layout, "Status", row.Status.ToPlainLabel(), COMPONENT_COLUMN_COUNT, ResolveStatusStyle(row.Status, row.NewerVersions.Count));
            AddLabeledValueRow(values, layout, "Releases Ahead", row.NewerVersions.Count.ToAheadReleasesLabel(), COMPONENT_COLUMN_COUNT);

            if (row.DetailsMessage.Value.HasDetails())
            {
                AddLabeledValueRow(values, layout, "Status Details", row.DetailsMessage.Value, COMPONENT_COLUMN_COUNT, ExcelCellStyleKind.AlertDetails);
            }

            AddBlankRow(values, COMPONENT_COLUMN_COUNT);
            AddRow(values, layout, ExcelCellStyleKind.SectionTitle, "A", "Newer Versions", COMPONENT_COLUMN_COUNT);

            var versionsHeaderRow = values.Count + 1;
            values.Add(CreateGridRow(
                COMPONENT_COLUMN_COUNT,
                "Version",
                "Jira Task",
                "Jira Title",
                "Jira Status",
                "Alerts"));

            var newerVersionsStartRow = versionsHeaderRow + 1;
            if (row.NewerVersions.Count == 0)
            {
                values.Add(CreateGridRow(
                    COMPONENT_COLUMN_COUNT,
                    "No newer versions available for this component."));
                layout.CellStyles[ToCellReference(1, values.Count)] = ExcelCellStyleKind.Muted;
            }
            else
            {
                foreach (var version in row.NewerVersions)
                {
                    var currentRow = values.Count + 1;
                    values.Add(CreateGridRow(
                        COMPONENT_COLUMN_COUNT,
                        version.Version.Value,
                        version.JiraTask.Value,
                        version.JiraTitle.Value,
                        version.JiraStatus.Value,
                        BuildAlertLabel(version)));

                    if (version.PullRequestUrl is not null)
                    {
                        layout.Hyperlinks[ToCellReference(1, currentRow)] = version.PullRequestUrl.ToString();
                        layout.CellStyles[ToCellReference(1, currentRow)] = ExcelCellStyleKind.Hyperlink;
                    }

                    if (TryBuildSingleTaskUrl(version.JiraTask.Value, out var taskUrl))
                    {
                        layout.Hyperlinks[ToCellReference(2, currentRow)] = taskUrl;
                        layout.CellStyles[ToCellReference(2, currentRow)] = ExcelCellStyleKind.Hyperlink;
                    }

                    layout.CellStyles[ToCellReference(5, currentRow)] = ResolveAlertStyle(version);
                }
            }

            var versionsDataEndRow = values.Count;
            layout.TableRanges.Add(new TableRange(
                versionsHeaderRow,
                1,
                5,
                newerVersionsStartRow,
                Math.Max(newerVersionsStartRow, versionsDataEndRow)));

            var taskDetails = row.NewerVersions.BuildTaskAlertDetailsByTask();
            AppendAlertSection(
                values,
                layout,
                "Breaking Changes",
                [.. taskDetails.Where(static detail => detail.BreakingChangesDetails.HasDetails())],
                static detail => detail.BreakingChangesDetails ?? string.Empty,
                ExcelCellStyleKind.AlertBreakingChanges);

            AppendAlertSection(
                values,
                layout,
                "Required Actions",
                [.. taskDetails.Where(static detail => detail.RequiredActionsDetails.HasDetails())],
                static detail => detail.RequiredActionsDetails ?? string.Empty,
                ExcelCellStyleKind.AlertRequiredActions);

            yield return new BuiltSheet(sheetName, values, layout);
        }
    }

    private void AppendAlertSection(
        List<Dictionary<string, object?>> values,
        SheetLayout layout,
        string title,
        JiraTaskAlertDetails[] taskDetails,
        Func<JiraTaskAlertDetails, string> detailsSelector,
        ExcelCellStyleKind titleStyle)
    {
        AddBlankRow(values, COMPONENT_COLUMN_COUNT);
        AddRow(values, layout, ExcelCellStyleKind.SectionTitle, "A", title, COMPONENT_COLUMN_COUNT);

        if (taskDetails.Length == 0)
        {
            AddRow(values, layout, ExcelCellStyleKind.Muted, "A", "No details available.", COMPONENT_COLUMN_COUNT);
            return;
        }

        var headerRow = values.Count + 1;
        values.Add(CreateGridRow(
            COMPONENT_COLUMN_COUNT,
            "Jira Task",
            "Jira Title",
            "Jira Status",
            "Details"));
        layout.TableRanges.Add(new TableRange(headerRow, 1, 4, headerRow + 1, headerRow + taskDetails.Length));

        foreach (var taskDetail in taskDetails)
        {
            var currentRow = values.Count + 1;
            values.Add(CreateGridRow(
                COMPONENT_COLUMN_COUNT,
                taskDetail.Task.Value,
                taskDetail.Title.Value,
                taskDetail.Status.Value,
                detailsSelector(taskDetail)));

            var taskCellReference = ToCellReference(1, currentRow);
            if (TryBuildSingleTaskUrl(taskDetail.Task.Value, out var taskUrl))
            {
                layout.Hyperlinks[taskCellReference] = taskUrl;
                layout.CellStyles[taskCellReference] = ExcelCellStyleKind.HyperlinkBold;
            }
            else
            {
                layout.CellStyles[taskCellReference] = titleStyle;
            }

            layout.CellStyles[ToCellReference(4, currentRow)] = ExcelCellStyleKind.AlertDetails;
            ApplyLinksWithinDetails(layout, 4, currentRow, detailsSelector(taskDetail));
        }
    }

    private void ApplyLinksWithinDetails(SheetLayout layout, int columnIndex, int rowIndex, string details)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(details);

        var cellReference = ToCellReference(columnIndex, rowIndex);

        foreach (Match browseUrlMatch in JiraBrowseUrlRegex().Matches(details))
        {
            layout.Hyperlinks[cellReference] = browseUrlMatch.Value;
            layout.Comments[cellReference] = "Contains Jira link: " + browseUrlMatch.Value;
            return;
        }

        foreach (Match taskMatch in JiraTaskKeyRegex().Matches(details))
        {
            if (TryBuildSingleTaskUrl(taskMatch.Groups["task"].Value, out var taskUrl))
            {
                layout.Hyperlinks[cellReference] = taskUrl;
                layout.Comments[cellReference] = "Contains Jira link: " + taskUrl;
                return;
            }
        }
    }

    private static ExcelCellStyleKind ResolveAlertStyle(VersionJiraRow version)
    {
        if (version.HasBreakingChanges)
        {
            return ExcelCellStyleKind.AlertBreakingChanges;
        }

        if (version.HasRequiredActions)
        {
            return ExcelCellStyleKind.AlertRequiredActions;
        }

        if (version.HasDependencyIssues)
        {
            return ExcelCellStyleKind.AlertDependency;
        }

        return ExcelCellStyleKind.Muted;
    }

    private static ExcelCellStyleKind ResolveStatusStyle(CheckStatus status, int newerVersionCount)
    {
        var hexColor = PdfPresentationHelpers.ResolveCheckStatusHexColor(status, newerVersionCount);
        return string.Equals(hexColor, "#15803d", StringComparison.OrdinalIgnoreCase)
            ? ExcelCellStyleKind.StatusPositive
            : string.Equals(hexColor, "#6b7280", StringComparison.OrdinalIgnoreCase)
                ? ExcelCellStyleKind.Muted
                : status == CheckStatus.Outdated
                    ? ExcelCellStyleKind.StatusWarning
                    : ExcelCellStyleKind.StatusNegative;
    }

    private static string BuildAlertLabel(VersionJiraRow version)
    {
        var labels = new List<string>(3);

        if (version.HasRequiredActions)
        {
            labels.Add("RA");
        }

        if (version.HasBreakingChanges)
        {
            labels.Add("BC");
        }

        if (version.HasDependencyIssues)
        {
            labels.Add("D");
        }

        return labels.Count == 0 ? "-" : string.Join(" ", labels);
    }

    private static void ApplyWorkbookFormatting(string outputPath, IReadOnlyDictionary<string, SheetLayout> layouts)
    {
        using var spreadsheet = SpreadsheetDocument.Open(outputPath, true);
        var workbookPart = spreadsheet.WorkbookPart
            ?? throw new InvalidOperationException("Workbook part is missing.");
        var workbook = workbookPart.Workbook
            ?? throw new InvalidOperationException("Workbook is missing.");

        EnsureStylesheet(workbookPart);

        foreach (var sheet in workbook.Sheets?.OfType<Sheet>() ?? [])
        {
            if (!layouts.TryGetValue(sheet.Name?.Value ?? string.Empty, out var layout))
            {
                continue;
            }

            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
            var worksheet = worksheetPart.Worksheet
                ?? throw new InvalidOperationException("Worksheet is missing.");
            var sheetData = worksheet.GetFirstChild<SheetData>()
                ?? throw new InvalidOperationException("Worksheet is missing sheet data.");

            ApplyColumnWidths(worksheet, layout.ColumnWidths);
            ApplyTableStyles(sheetData, layout.TableRanges);
            ApplyCellStyles(sheetData, layout.CellStyles);
            ApplyComments(workbookPart, worksheetPart, layout.Comments);
            ApplyHyperlinks(worksheetPart, layout.Hyperlinks);

            worksheet.Save();
        }

        workbookPart.Workbook.Save();
    }

    private static void EnsureStylesheet(WorkbookPart workbookPart)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);

        var stylesPart = workbookPart.WorkbookStylesPart ?? workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = CreateStylesheet();
        stylesPart.Stylesheet.Save();
    }

    private static Stylesheet CreateStylesheet()
    {
        var fonts = new Fonts(
            new Font(),
            new Font(new Bold()),
            new Font(new Bold(), new FontSize { Val = 16D }),
            new Font(new Bold(), new FontSize { Val = 12D }),
            new Font(new Color { Rgb = LINK_COLOR_HEX }, new Underline()),
            new Font(new Bold(), new Color { Rgb = LINK_COLOR_HEX }, new Underline()),
            new Font(new Bold(), new Color { Rgb = ToRgb("15803d") }),
            new Font(new Bold(), new Color { Rgb = ToRgb("ea580c") }),
            new Font(new Bold(), new Color { Rgb = ToRgb("b91c1c") }),
            new Font(new Bold(), new Color { Rgb = ToRgb("ffaf00") }),
            new Font(new Bold(), new Color { Rgb = ToRgb("ff0000") }),
            new Font(new Bold(), new Color { Rgb = ToRgb("00afff") }),
            new Font(new Color { Rgb = MUTED_HEX }),
            new Font(new Bold(), new Color { Rgb = ToRgb("374151") }))
        {
            Count = 14U,
            KnownFonts = true,
        };

        var fills = new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(
                new ForegroundColor { Rgb = HEADER_FILL_HEX },
                new BackgroundColor { Indexed = 64U })
            { PatternType = PatternValues.Solid }),
            new Fill(new PatternFill(
                new ForegroundColor { Rgb = ALERT_DETAILS_FILL_HEX },
                new BackgroundColor { Indexed = 64U })
            { PatternType = PatternValues.Solid }))
        {
            Count = 4U,
        };

        var borders = new Borders(
            new Border(),
            new Border(
                new LeftBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = BORDER_HEX } },
                new RightBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = BORDER_HEX } },
                new TopBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = BORDER_HEX } },
                new BottomBorder { Style = BorderStyleValues.Thin, Color = new Color { Rgb = BORDER_HEX } },
                new DiagonalBorder()))
        {
            Count = 2U,
        };

        var cellStyleFormats = new CellStyleFormats(new CellFormat())
        {
            Count = 1U,
        };

        var cellFormats = new CellFormats(
            new CellFormat(),
            new CellFormat { FontId = 2U, ApplyFont = true },
            new CellFormat { FontId = 13U, ApplyFont = true },
            new CellFormat { FontId = 3U, ApplyFont = true },
            CreateBorderedFormat(fontId: 1U, fillId: 2U),
            CreateBorderedFormat(),
            CreateBorderedFormat(fontId: 6U),
            CreateBorderedFormat(fontId: 7U),
            CreateBorderedFormat(fontId: 8U),
            CreateBorderedFormat(fontId: 4U),
            CreateBorderedFormat(fontId: 5U),
            CreateBorderedFormat(fontId: 9U),
            CreateBorderedFormat(fontId: 10U),
            CreateBorderedFormat(fontId: 11U),
            CreateBorderedFormat(fillId: 3U),
            CreateBorderedFormat(fontId: 12U))
        {
            Count = 16U,
        };

        return new Stylesheet(fonts, fills, borders, cellStyleFormats, cellFormats);
    }

    private static CellFormat CreateBorderedFormat(uint fontId = 0U, uint fillId = 0U) =>
        new()
        {
            FontId = fontId,
            FillId = fillId,
            BorderId = 1U,
            ApplyFont = true,
            ApplyFill = fillId != 0U,
            ApplyBorder = true,
            ApplyAlignment = true,
            Alignment = new Alignment
            {
                Vertical = VerticalAlignmentValues.Top,
                WrapText = true,
            },
        };

    private static void ApplyColumnWidths(Worksheet worksheet, Dictionary<int, double> columnWidths)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(columnWidths);

        if (columnWidths.Count == 0)
        {
            return;
        }

        worksheet.RemoveAllChildren<Columns>();

        var columns = new Columns();
        foreach (var (columnIndex, width) in columnWidths.OrderBy(static x => x.Key))
        {
            columns.Append(new Column
            {
                Min = (uint)columnIndex,
                Max = (uint)columnIndex,
                Width = width,
                CustomWidth = true,
            });
        }

        var sheetData = worksheet.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException("Worksheet is missing sheet data.");
        _ = worksheet.InsertBefore(columns, sheetData);
    }

    private static void ApplyTableStyles(SheetData sheetData, IReadOnlyList<TableRange> tableRanges)
    {
        ArgumentNullException.ThrowIfNull(sheetData);
        ArgumentNullException.ThrowIfNull(tableRanges);

        foreach (var range in tableRanges)
        {
            for (var columnIndex = range.StartColumnIndex; columnIndex <= range.EndColumnIndex; columnIndex++)
            {
                SetCellStyle(sheetData, columnIndex, range.HeaderRow, ExcelCellStyleKind.Header);
            }

            for (var rowIndex = range.DataStartRow; rowIndex <= range.DataEndRow; rowIndex++)
            {
                for (var columnIndex = range.StartColumnIndex; columnIndex <= range.EndColumnIndex; columnIndex++)
                {
                    SetCellStyle(sheetData, columnIndex, rowIndex, ExcelCellStyleKind.Body);
                }
            }
        }
    }

    private static void ApplyCellStyles(SheetData sheetData, IReadOnlyDictionary<string, ExcelCellStyleKind> cellStyles)
    {
        ArgumentNullException.ThrowIfNull(sheetData);
        ArgumentNullException.ThrowIfNull(cellStyles);

        foreach (var (cellReference, styleKind) in cellStyles)
        {
            var (columnIndex, rowIndex) = ParseCellReference(cellReference);
            SetCellStyle(sheetData, columnIndex, rowIndex, styleKind);
        }
    }

    private static void ApplyHyperlinks(WorksheetPart worksheetPart, Dictionary<string, string> hyperlinks)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(hyperlinks);

        if (hyperlinks.Count == 0)
        {
            return;
        }

        var worksheet = worksheetPart.Worksheet
            ?? throw new InvalidOperationException("Worksheet is missing.");
        var hyperlinksElement = worksheet.GetFirstChild<Hyperlinks>();
        if (hyperlinksElement is null)
        {
            hyperlinksElement = new Hyperlinks();
            var pageMargins = worksheet.GetFirstChild<PageMargins>();
            if (pageMargins is not null)
            {
                _ = worksheet.InsertBefore(hyperlinksElement, pageMargins);
            }
            else
            {
                _ = worksheet.AppendChild(hyperlinksElement);
            }
        }

        foreach (var (cellReference, targetUrl) in hyperlinks)
        {
            var relationship = worksheetPart.AddHyperlinkRelationship(new Uri(targetUrl, UriKind.Absolute), true);
            _ = hyperlinksElement.AppendChild(new Hyperlink
            {
                Reference = cellReference,
                Id = relationship.Id,
            });
        }
    }

    private static void ApplyComments(
        WorkbookPart workbookPart,
        WorksheetPart worksheetPart,
        Dictionary<string, string> comments)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(comments);

        if (comments.Count == 0)
        {
            return;
        }

        var commentsPart = worksheetPart.WorksheetCommentsPart ?? worksheetPart.AddNewPart<WorksheetCommentsPart>();
        commentsPart.Comments ??= new Comments(
            new Authors(new Author("NReleaseBuilder")),
            new CommentList());

        var commentList = commentsPart.Comments.GetFirstChild<CommentList>()
            ?? commentsPart.Comments.AppendChild(new CommentList());

        foreach (var (cellReference, commentText) in comments)
        {
            _ = commentList.AppendChild(new Comment
            {
                Reference = cellReference,
                AuthorId = 0U,
                CommentText = new CommentText(new Run(new Text(commentText))),
            });
        }
    }

    private static void SetCellStyle(SheetData sheetData, int columnIndex, int rowIndex, ExcelCellStyleKind styleKind)
    {
        var cell = GetOrCreateCell(sheetData, columnIndex, rowIndex);
        cell.StyleIndex = (uint)styleKind;
    }

    private static Cell GetOrCreateCell(SheetData sheetData, int columnIndex, int rowIndex)
    {
        ArgumentNullException.ThrowIfNull(sheetData);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnIndex, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowIndex, 1);

        var row = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value == (uint)rowIndex);
        if (row is null)
        {
            row = new Row { RowIndex = (uint)rowIndex };

            var nextRow = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value > (uint)rowIndex);
            if (nextRow is null)
            {
                _ = sheetData.AppendChild(row);
            }
            else
            {
                _ = sheetData.InsertBefore(row, nextRow);
            }
        }

        var cellReference = ToCellReference(columnIndex, rowIndex);
        var cell = row.Elements<Cell>().FirstOrDefault(c => string.Equals(c.CellReference?.Value, cellReference, StringComparison.OrdinalIgnoreCase));
        if (cell is not null)
        {
            return cell;
        }

        cell = new Cell
        {
            CellReference = cellReference,
            DataType = CellValues.String,
            CellValue = new CellValue(string.Empty),
        };

        var nextCell = row.Elements<Cell>()
            .FirstOrDefault(c => CompareCellReferences(c.CellReference?.Value, cellReference) > 0);

        if (nextCell is null)
        {
            _ = row.AppendChild(cell);
        }
        else
        {
            _ = row.InsertBefore(cell, nextCell);
        }

        return cell;
    }

    private static int CompareCellReferences(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return 1;
        }

        var (leftColumn, leftRow) = ParseCellReference(left);
        var (rightColumn, rightRow) = ParseCellReference(right);

        var rowComparison = leftRow.CompareTo(rightRow);
        return rowComparison != 0 ? rowComparison : leftColumn.CompareTo(rightColumn);
    }

    private static (int ColumnIndex, int RowIndex) ParseCellReference(string cellReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cellReference);

        var column = 0;
        var index = 0;
        while (index < cellReference.Length && char.IsLetter(cellReference[index]))
        {
            var letterValue = char.ToUpperInvariant(cellReference[index]) - 'A' + 1;
            column = (column * 26) + letterValue;
            index++;
        }

        var row = int.Parse(cellReference[index..], CultureInfo.InvariantCulture);
        return (column, row);
    }

    private static string ToCellReference(int columnIndex, int rowIndex) =>
        ToColumnName(columnIndex) + rowIndex.ToString(CultureInfo.InvariantCulture);

    private static string ToColumnName(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columnIndex, 1);

        var dividend = columnIndex;
        var columnName = string.Empty;

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo, CultureInfo.InvariantCulture) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static Dictionary<string, object?> CreateGridRow(int columnCount, params object?[] values)
    {
        var row = new Dictionary<string, object?>(columnCount, StringComparer.Ordinal);

        for (var columnIndex = 1; columnIndex <= columnCount; columnIndex++)
        {
            row.Add(
                "C" + columnIndex.ToString(CultureInfo.InvariantCulture),
                columnIndex <= values.Length ? values[columnIndex - 1] ?? string.Empty : string.Empty);
        }

        return row;
    }

    private static void AddBlankRow(List<Dictionary<string, object?>> values, int columnCount) =>
        values.Add(CreateGridRow(columnCount, string.Empty));

    private static void AddRow(
        List<Dictionary<string, object?>> values,
        SheetLayout layout,
        ExcelCellStyleKind styleKind,
        string columnName,
        string value,
        int columnCount)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        ArgumentNullException.ThrowIfNull(value);

        var row = CreateGridRow(columnCount, string.Empty);
        row["C" + ColumnNameToIndex(columnName).ToString(CultureInfo.InvariantCulture)] = value;
        values.Add(row);
        layout.CellStyles[columnName + values.Count.ToString(CultureInfo.InvariantCulture)] = styleKind;
    }

    private static void AddLabeledValueRow(
        List<Dictionary<string, object?>> values,
        SheetLayout layout,
        string label,
        string value,
        int columnCount,
        ExcelCellStyleKind? valueStyle = null)
    {
        values.Add(CreateGridRow(columnCount, label, value));
        var rowIndex = values.Count;
        layout.CellStyles["A" + rowIndex.ToString(CultureInfo.InvariantCulture)] = ExcelCellStyleKind.MetadataLabel;

        if (valueStyle is not null)
        {
            layout.CellStyles["B" + rowIndex.ToString(CultureInfo.InvariantCulture)] = valueStyle.Value;
        }
    }

    private static string BuildUniqueSheetName(string baseName, HashSet<string> usedNames)
    {
        var sanitized = SanitizeSheetName(baseName);
        var candidate = sanitized;
        var suffix = 2;

        while (!usedNames.Add(candidate))
        {
            var suffixValue = "_" + suffix.ToString(CultureInfo.InvariantCulture);
            var maxBaseLength = 31 - suffixValue.Length;
            candidate = sanitized[..Math.Min(sanitized.Length, maxBaseLength)] + suffixValue;
            suffix++;
        }

        return candidate;
    }

    private static string SanitizeSheetName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        HashSet<char> invalidChars = ['\\', '/', '?', '*', '[', ']', ':'];
        var filteredChars = value.Where(ch => !invalidChars.Contains(ch)).ToArray();
        var filtered = new string(filteredChars).Trim();
        if (filtered.Length == 0)
        {
            filtered = "Component";
        }

        return filtered.Length <= 31 ? filtered : filtered[..31];
    }

    private static int ColumnNameToIndex(string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        var result = 0;
        foreach (var symbol in columnName)
        {
            var letterValue = char.ToUpperInvariant(symbol) - 'A' + 1;
            result = (result * 26) + letterValue;
        }

        return result;
    }

    private bool TryBuildSingleTaskUrl(string taskValue, out string taskUrl)
    {
        var taskValues = SplitTaskValues(taskValue);
        if (taskValues.Length != 1 || !IsTrackableJiraTask(taskValues[0]))
        {
            taskUrl = string.Empty;
            return false;
        }

        taskUrl = new Uri(_jiraBrowseBaseUrl, "browse/" + taskValues[0]).ToString();
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

    [GeneratedRegex(
        @"https?://[^\s\)\]\}<>""']+/browse/(?<task>[A-Za-z][A-Za-z0-9_]*-\d+)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex JiraBrowseUrlRegex();

    [GeneratedRegex(
        @"(?<![A-Za-z0-9_])(?<task>[A-Za-z][A-Za-z0-9_]*-\d+)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant)]
    private static partial Regex JiraTaskKeyRegex();

    private static string ToRgb(string value) => value.TrimStart('#').ToUpperInvariant();

    private readonly AppSettings _settings;
    private readonly Uri _jiraBrowseBaseUrl;

    private sealed record WorkbookData(
        IReadOnlyDictionary<string, object> Sheets,
        IReadOnlyDictionary<string, SheetLayout> Layouts);

    private sealed record BuiltSheet(
        string Name,
        List<Dictionary<string, object?>> Rows,
        SheetLayout Layout);

    private sealed class SheetLayout(string name)
    {
        public string Name { get; set; } = name;

        public Dictionary<int, double> ColumnWidths { get; } = [];

        public List<TableRange> TableRanges { get; } = [];

        public Dictionary<string, ExcelCellStyleKind> CellStyles { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Hyperlinks { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Comments { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record TableRange(
        int HeaderRow,
        int StartColumnIndex,
        int EndColumnIndex,
        int DataStartRow,
        int DataEndRow);

    private enum ExcelCellStyleKind : uint
    {
        Default = 0,
        Title = 1,
        MetadataLabel = 2,
        SectionTitle = 3,
        Header = 4,
        Body = 5,
        StatusPositive = 6,
        StatusWarning = 7,
        StatusNegative = 8,
        Hyperlink = 9,
        HyperlinkBold = 10,
        AlertRequiredActions = 11,
        AlertBreakingChanges = 12,
        AlertDependency = 13,
        AlertDetails = 14,
        Muted = 15,
    }
}
