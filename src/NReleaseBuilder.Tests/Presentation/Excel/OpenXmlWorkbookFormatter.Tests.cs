using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

using FluentAssertions;

using Microsoft.Extensions.Options;

using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Rendering;
using NReleaseBuilder.Presentation.Excel;

namespace NReleaseBuilder.Tests.Presentation.Excel;

public class OpenXmlWorkbookFormatterTests
{
    [Fact(DisplayName = "OpenXmlWorkbookFormatter can be created.")]
    [Trait("Category", "Unit")]
    public void OpenXmlWorkbookFormatterCanBeCreated()
    {
        // Arrange
        // Act
        var exception = Record.Exception(() => _ = new OpenXmlWorkbookFormatter());

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "OpenXmlWorkbookFormatter Format validates null arguments.")]
    [Trait("Category", "Unit")]
    public void FormatValidatesNullArguments()
    {
        // Arrange
        var sut = new OpenXmlWorkbookFormatter();
        var layouts = new Dictionary<string, ExcelSheetLayout>(StringComparer.Ordinal);

        // Act
        Action nullStream = () => sut.Format(null!, layouts);
        Action nullLayouts = () => sut.Format(new MemoryStream(), null!);

        // Assert
        nullStream.Should().Throw<ArgumentNullException>()
            .WithParameterName("workbookStream");
        nullLayouts.Should().Throw<ArgumentNullException>()
            .WithParameterName("layouts");
    }

    [Fact(DisplayName = "OpenXmlWorkbookFormatter Format applies styles hyperlinks and comments.")]
    [Trait("Category", "Integration")]
    public void FormatAppliesStylesHyperlinksAndComments()
    {
        // Arrange
        var store = new ExcelReportFileStore(Options.Create(CreateSettings()));
        var sut = new OpenXmlWorkbookFormatter();
        IReadOnlyDictionary<string, object> sheets = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Summary"] = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal)
                {
                    ["C1"] = "Header",
                    ["C2"] = "Link",
                },
                new(StringComparer.Ordinal)
                {
                    ["C1"] = "Body",
                    ["C2"] = "Value",
                },
            },
        };

        var layout = new ExcelSheetLayout("Summary");
        layout.ColumnWidths[1] = 20;
        layout.TableRanges.Add(new ExcelTableRange(1, 1, 2, 2, 2));
        layout.CellStyles["B2"] = ExcelCellStyleKind.Hyperlink;
        layout.Hyperlinks["B2"] = "https://example.test/task/123";
        layout.Comments["B2"] = "Contains Jira link: https://example.test/task/123";

        using var stream = store.CreateWorkbookStream(sheets);

        // Act
        sut.Format(
            stream,
            new Dictionary<string, ExcelSheetLayout>(StringComparer.Ordinal)
            {
                ["Summary"] = layout,
            });

        // Assert
        stream.Position.Should().Be(0);

        using var workbook = SpreadsheetDocument.Open(stream, false);
        workbook.WorkbookPart.Should().NotBeNull();
        var workbookPart = workbook.WorkbookPart!;
        workbookPart.Workbook.Should().NotBeNull();
        var workbookRoot = workbookPart.Workbook!;
        workbookRoot.Sheets.Should().NotBeNull();
        var sheet = workbookRoot.Sheets!.OfType<Sheet>().Single();
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        worksheetPart.Worksheet.Should().NotBeNull();
        var worksheet = worksheetPart.Worksheet!;

        worksheet.Elements<Columns>().Should().NotBeEmpty();
        worksheet.Descendants<Hyperlink>().Any(x => string.Equals(x.Reference?.Value, "B2", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        worksheetPart.WorksheetCommentsPart.Should().NotBeNull();

        var cellB2 = worksheet.Descendants<Cell>()
            .Single(cell => string.Equals(cell.CellReference?.Value, "B2", StringComparison.OrdinalIgnoreCase));
        cellB2.StyleIndex.Should().Be((uint)ExcelCellStyleKind.Hyperlink);
    }

    private static AppSettings CreateSettings() =>
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
            },
            Pdf = new PdfOptions
            {
                Enabled = false,
                OutputPath = "report.pdf",
            },
            Excel = new ExcelOptions
            {
                Enabled = true,
                OutputPath = "report.xlsx",
            },
        };
}
