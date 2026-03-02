using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Models.Rendering;
using NReleaseBuilder.Presentation.Pdf;

namespace NReleaseBuilder.Presentation.Excel;

/// <summary>
/// Default implementation for composing Excel workbook content.
/// </summary>
public sealed partial class MiniExcelContentComposer : IExcelContentComposer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MiniExcelContentComposer"/> class.
    /// </summary>
    /// <param name="options">Application settings options.</param>
    public MiniExcelContentComposer(IOptions<AppSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _settings = options.Value;
        _jiraBrowseBaseUrl = BuildBaseJiraUrl(_settings.Jira.BaseUrl);
    }

    /// <inheritdoc />
    public ExcelWorkbookData ComposeWorkbook(
        ComponentCheckRow[] rows,
        JiraStatusName[] allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(allowedStatuses);
        ArgumentNullException.ThrowIfNull(statusStatistics);

        var sheets = new Dictionary<string, object>(StringComparer.Ordinal);
        var layouts = new Dictionary<string, ExcelSheetLayout>(StringComparer.Ordinal);

        var summarySheet = BuildSummarySheet(rows, allowedStatuses, statusStatistics);
        sheets.Add(summarySheet.Name, summarySheet.Rows);
        layouts.Add(summarySheet.Name, summarySheet.Layout);

        foreach (var componentSheet in BuildComponentSheets(rows))
        {
            sheets.Add(componentSheet.Name, componentSheet.Rows);
            layouts.Add(componentSheet.Name, componentSheet.Layout);
        }

        return new ExcelWorkbookData(sheets, layouts);
    }

    private BuiltSheet BuildSummarySheet(
        ComponentCheckRow[] rows,
        JiraStatusName[] allowedStatuses,
        IReadOnlyDictionary<JiraStatusName, int> statusStatistics)
    {
        var values = new List<Dictionary<string, object?>>();
        var layout = new ExcelSheetLayout(SUMMARY_SHEET_NAME)
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

        if (rows.Length == 0)
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
        layout.TableRanges.Add(new ExcelTableRange(resultsHeaderRow, 1, 5, resultsHeaderRow + 1, resultsHeaderRow + rows.Length));

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

        layout.TableRanges.Add(new ExcelTableRange(chartHeaderRow, 1, 2, chartHeaderRow + 1, chartHeaderRow + orderedEntries.Length));

        foreach (var (status, taskCount) in orderedEntries)
        {
            values.Add(CreateGridRow(
                SUMMARY_COLUMN_COUNT,
                status.Value,
                taskCount.ToString(CultureInfo.InvariantCulture)));
        }

        return new BuiltSheet(SUMMARY_SHEET_NAME, values, layout);
    }

    private IEnumerable<BuiltSheet> BuildComponentSheets(ComponentCheckRow[] rows)
    {
        var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SUMMARY_SHEET_NAME,
        };

        foreach (var row in rows)
        {
            var values = new List<Dictionary<string, object?>>();
            var layout = new ExcelSheetLayout(string.Empty)
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
            layout.TableRanges.Add(new ExcelTableRange(
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
        ExcelSheetLayout layout,
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
        layout.TableRanges.Add(new ExcelTableRange(headerRow, 1, 4, headerRow + 1, headerRow + taskDetails.Length));

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

    private void ApplyLinksWithinDetails(ExcelSheetLayout layout, int columnIndex, int rowIndex, string details)
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
        ExcelSheetLayout layout,
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
        ExcelSheetLayout layout,
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

    private readonly AppSettings _settings;
    private readonly Uri _jiraBrowseBaseUrl;

    private sealed record BuiltSheet(
        string Name,
        List<Dictionary<string, object?>> Rows,
        ExcelSheetLayout Layout);

    private const int SUMMARY_COLUMN_COUNT = 8;
    private const int COMPONENT_COLUMN_COUNT = 5;
    private const string SUMMARY_SHEET_NAME = "Summary";
}
