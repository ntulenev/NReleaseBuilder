using System.Globalization;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using FluentAssertions;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Presentation.Excel;

namespace NReleaseBuilder.Tests.Presentation.Excel;

public class MiniExcelReportRendererTests
{
    [Fact(DisplayName = "MiniExcelReportRenderer skips rendering when Excel is disabled.")]
    [Trait("Category", "Unit")]
    public void RenderReportSkipsRenderingWhenExcelIsDisabled()
    {
        // Arrange
        var tempDirectory = CreateTempDirectoryPath();
        var settings = CreateSettings(tempDirectory, excelEnabled: false);
        var sut = new MiniExcelReportRenderer(Options.Create(settings));
        var rows = new[] { CreateRow() };
        var statuses = new[] { new JiraStatusName("Done") };
        IReadOnlyDictionary<JiraStatusName, int> statistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("Done")] = 1,
        };

        // Act
        sut.RenderReport(rows, statuses, statistics);

        // Assert
        Directory.Exists(tempDirectory).Should().BeFalse();
    }

    [Fact(DisplayName = "MiniExcelReportRenderer creates summary and component sheets with links and alert sections.")]
    [Trait("Category", "Integration")]
    public void RenderReportCreatesWorkbookWithExpectedSheetsAndLinks()
    {
        // Arrange
        using var tempDirectory = new TempDirectory();
        var settings = CreateSettings(tempDirectory.Path, excelEnabled: true);
        var sut = new MiniExcelReportRenderer(Options.Create(settings));
        var rows = new[] { CreateRow() };
        var statuses = new[] { new JiraStatusName("Done") };
        IReadOnlyDictionary<JiraStatusName, int> statistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("Done")] = 1,
        };

        var dateSuffix = DateTime.Now.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var expectedOutputPath = Path.Combine(tempDirectory.Path, $"report_{dateSuffix}.xlsx");

        // Act
        sut.RenderReport(rows, statuses, statistics);

        // Assert
        File.Exists(expectedOutputPath).Should().BeTrue();

        using var workbook = SpreadsheetDocument.Open(expectedOutputPath, false);
        var workbookPart = workbook.WorkbookPart;
        workbookPart.Should().NotBeNull();
        var workbookRoot = workbookPart!.Workbook;
        workbookRoot.Should().NotBeNull();

        var sheets = workbookRoot!.Sheets!.OfType<Sheet>().ToArray();

        sheets.Should().HaveCount(2);
        sheets.Select(static sheet => sheet.Name!.Value).Should().Contain("Summary");
        sheets.Select(static sheet => sheet.Name!.Value).Should().ContainSingle(name => name.Contains("component-api", StringComparison.OrdinalIgnoreCase));

        var summarySheet = sheets.Single(sheet => string.Equals(sheet.Name!.Value, "Summary", StringComparison.Ordinal));
        var summaryWorksheetPart = (WorksheetPart)workbookPart.GetPartById(summarySheet.Id!);
        GetCellText(workbookPart, summaryWorksheetPart, "A1").Should().Be("Components Version Check");
        GetCellText(workbookPart, summaryWorksheetPart, "A7").Should().Be("Results");
        GetCellText(workbookPart, summaryWorksheetPart, "A11").Should().Be("Unique Jira Tasks By Status");

        var componentSheet = sheets.Single(sheet => !string.Equals(sheet.Name!.Value, "Summary", StringComparison.Ordinal));
        var componentWorksheetPart = (WorksheetPart)workbookPart.GetPartById(componentSheet.Id!);

        GetCellText(workbookPart, componentWorksheetPart, "A1").Should().Be("1. component-api");
        GetCellText(workbookPart, componentWorksheetPart, "A7").Should().Be("Newer Versions");
        GetCellText(workbookPart, componentWorksheetPart, "E8").Should().Be("Alerts");
        GetAllCellTexts(workbookPart, componentWorksheetPart).Should().Contain("Breaking Changes");
        GetAllCellTexts(workbookPart, componentWorksheetPart).Should().Contain("Required Actions");

        var componentWorksheet = componentWorksheetPart.Worksheet;
        componentWorksheet.Should().NotBeNull();
        componentWorksheet!.Descendants<Hyperlink>().Should().HaveCountGreaterOrEqualTo(3);

        var commentsPart = componentWorksheetPart.WorksheetCommentsPart;
        commentsPart.Should().NotBeNull();
        commentsPart!.Comments.Should().NotBeNull();
        commentsPart.Comments!.Descendants<Comment>().Should().HaveCount(2);
    }

    private static AppSettings CreateSettings(string outputDirectory, bool excelEnabled) =>
        new()
        {
            CsvFilePath = "components.csv",
            CsvComponentNamesFilter = [],
            Bitbucket = new BitbucketOptions
            {
                BaseUrl = new Uri("https://bitbucket.example.test/"),
                Workspace = "workspace",
                ProjectNames = ["PROJ"],
                AuthEmail = "bot@example.test",
                AuthApiToken = "token",
            },
            Jira = new JiraOptions
            {
                BaseUrl = new Uri("https://jira.example.test/"),
                Email = "jira@example.test",
                ApiToken = "token",
                AllowedTaskStatuses = ["Done"],
                RequiredActionsFieldName = "Required Actions",
                BreakingChangesFieldName = "Breaking changes",
            },
            Pdf = new PdfOptions
            {
                Enabled = false,
                OutputPath = "report.pdf",
            },
            Excel = new ExcelOptions
            {
                Enabled = excelEnabled,
                OutputPath = Path.Combine(outputDirectory, "report.xlsx"),
            },
        };

    private static ComponentCheckRow CreateRow()
    {
        var jiraTask = new JiraTaskReference("APP-42");
        var jiraStatus = new JiraStatusReference("Done");
        var jiraTitle = new JiraTitleReference("Improve release report");

        var version = new VersionJiraRow(
            new VersionLabel("2.0.0"),
            jiraTask,
            jiraTitle,
            jiraStatus,
            [
                new JiraTaskAlertDetails(
                    jiraTask,
                    jiraTitle,
                    jiraStatus,
                    "Review deployment steps for APP-42",
                    "Breaking change details in https://jira.example.test/browse/APP-42"),
            ],
            hasRequiredActions: true,
            hasBreakingChanges: true,
            hasDependencyIssues: true,
            pullRequestUrl: new Uri("https://bitbucket.example.test/projects/PROJ/repos/repo/pull-requests/42"));

        return new ComponentCheckRow(
            new ComponentCheckIndex(1),
            new ComponentName("component-api"),
            new RepositoryName("repo-api"),
            new VersionLabel("1.0.0"),
            CheckStatus.Outdated,
            new RowDetails("-"),
            [version]);
    }

    private static string GetCellText(WorkbookPart workbookPart, WorksheetPart worksheetPart, string cellReference)
    {
        var worksheet = worksheetPart.Worksheet;
        worksheet.Should().NotBeNull();

        var cell = worksheet!.Descendants<Cell>()
            .Single(c => string.Equals(c.CellReference!.Value, cellReference, StringComparison.OrdinalIgnoreCase));

        return ResolveCellValue(workbookPart, cell);
    }

    private static string[] GetAllCellTexts(WorkbookPart workbookPart, WorksheetPart worksheetPart) =>
    [
        .. worksheetPart.Worksheet!.Descendants<Cell>()
            .Select(cell => ResolveCellValue(workbookPart, cell))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
    ];

    private static string ResolveCellValue(WorkbookPart workbookPart, Cell cell)
    {
        var rawValue = cell.CellValue?.Text ?? string.Empty;
        if (cell.DataType?.Value != CellValues.SharedString)
        {
            return rawValue;
        }

        var sharedStringPart = workbookPart.SharedStringTablePart;
        sharedStringPart.Should().NotBeNull();
        sharedStringPart!.SharedStringTable.Should().NotBeNull();
        return sharedStringPart.SharedStringTable!
            .ElementAt(int.Parse(rawValue, CultureInfo.InvariantCulture))
            .InnerText;
    }

    private static string CreateTempDirectoryPath() =>
        Path.Combine(Path.GetTempPath(), $"nrb-excel-tests-{Guid.NewGuid():N}");

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = CreateTempDirectoryPath();
            _ = Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
