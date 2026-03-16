using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Models.Rendering;
using NReleaseBuilder.Presentation.Excel;

namespace NReleaseBuilder.Tests.Presentation.Excel;

public class MiniExcelReportRendererTests
{
    [Fact(DisplayName = "MiniExcelReportRenderer can be created.")]
    [Trait("Category", "Unit")]
    public void MiniExcelReportRendererCanBeCreated()
    {
        // Arrange
        var settings = CreateSettings("reports", excelEnabled: true);
        var options = Options.Create(settings);
        var composer = new Mock<IExcelContentComposer>(MockBehavior.Strict).Object;
        var fileStore = new Mock<IExcelReportFileStore>(MockBehavior.Strict).Object;
        var formatter = new Mock<IWorkbookFormatter>(MockBehavior.Strict).Object;
        var reportRunContextAccessor = CreateReportRunContextAccessor();

        // Act
        var exception = Record.Exception(() => _ = new MiniExcelReportRenderer(
            options,
            composer,
            fileStore,
            formatter,
            reportRunContextAccessor));

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "MiniExcelReportRenderer skips rendering when Excel is disabled.")]
    [Trait("Category", "Unit")]
    public void RenderReportSkipsRenderingWhenExcelIsDisabled()
    {
        // Arrange
        var tempDirectory = CreateTempDirectoryPath();
        var settings = CreateSettings(tempDirectory, excelEnabled: false);
        var composer = new Mock<IExcelContentComposer>(MockBehavior.Strict).Object;
        var fileStore = new Mock<IExcelReportFileStore>(MockBehavior.Strict).Object;
        var formatter = new Mock<IWorkbookFormatter>(MockBehavior.Strict).Object;
        var sut = new MiniExcelReportRenderer(
            Options.Create(settings),
            composer,
            fileStore,
            formatter,
            CreateReportRunContextAccessor());

        // Act
        sut.RenderReport([CreateRow()], [new JiraStatusName("Done")], CreateStatistics());

        // Assert
        Directory.Exists(tempDirectory).Should().BeFalse();
    }

    [Fact(DisplayName = "MiniExcelReportRenderer RenderReport validates null arguments.")]
    [Trait("Category", "Unit")]
    public void RenderReportValidatesNullArguments()
    {
        // Arrange
        var settings = CreateSettings("reports", excelEnabled: false);
        var composer = new Mock<IExcelContentComposer>(MockBehavior.Strict).Object;
        var fileStore = new Mock<IExcelReportFileStore>(MockBehavior.Strict).Object;
        var formatter = new Mock<IWorkbookFormatter>(MockBehavior.Strict).Object;
        var sut = new MiniExcelReportRenderer(
            Options.Create(settings),
            composer,
            fileStore,
            formatter,
            CreateReportRunContextAccessor());

        // Act
        Action nullRows = () => sut.RenderReport(null!, [], CreateStatistics());
        Action nullStatuses = () => sut.RenderReport([], null!, CreateStatistics());
        Action nullStatistics = () => sut.RenderReport([], [], null!);

        // Assert
        nullRows.Should().Throw<ArgumentNullException>()
            .WithParameterName("rows");
        nullStatuses.Should().Throw<ArgumentNullException>()
            .WithParameterName("allowedStatuses");
        nullStatistics.Should().Throw<ArgumentNullException>()
            .WithParameterName("statusStatistics");
    }

    [Fact(DisplayName = "MiniExcelReportRenderer composes formats and delegates workbook persistence when Excel is enabled.")]
    [Trait("Category", "Unit")]
    public void RenderReportComposesFormatsAndDelegatesWorkbookPersistenceWhenExcelIsEnabled()
    {
        // Arrange
        var settings = CreateSettings("reports", excelEnabled: true);
        var rows = new[] { CreateRow() };
        var statuses = new[] { new JiraStatusName("Done") };
        var statistics = CreateStatistics();
        var expectedOutputPath = settings.Excel.ResolveOutputPath();
        var workbookData = CreateWorkbookData();
        using var workbookStream = new ExcelReportFileStore(Options.Create(settings)).CreateWorkbookStream(workbookData.Sheets);

        var composeCalls = 0;
        var createWorkbookStreamCalls = 0;
        var formatCalls = 0;
        var saveCalls = 0;

        var composerMock = new Mock<IExcelContentComposer>(MockBehavior.Strict);
        composerMock
            .Setup(x => x.ComposeWorkbook(
                It.Is<ComponentCheckRow[]>(inputRows => ReferenceEquals(inputRows, rows)),
                It.Is<JiraStatusName[]>(inputStatuses => ReferenceEquals(inputStatuses, statuses)),
                It.Is<IReadOnlyDictionary<JiraStatusName, int>>(inputStatistics => ReferenceEquals(inputStatistics, statistics))))
            .Callback(() => composeCalls++)
            .Returns(workbookData);

        var fileStoreMock = new Mock<IExcelReportFileStore>(MockBehavior.Strict);
        fileStoreMock
            .Setup(x => x.CreateWorkbookStream(
                It.Is<IReadOnlyDictionary<string, object>>(sheets => ReferenceEquals(sheets, workbookData.Sheets))))
            .Callback(() => createWorkbookStreamCalls++)
            .Returns(workbookStream);
        fileStoreMock
            .Setup(x => x.Save(
                It.Is<Stream>(stream => ReferenceEquals(stream, workbookStream)),
                It.Is<string?>(outputPathOverride => outputPathOverride == null)))
            .Callback(() => saveCalls++)
            .Returns(expectedOutputPath);

        var formatterMock = new Mock<IWorkbookFormatter>(MockBehavior.Strict);
        formatterMock
            .Setup(x => x.Format(
                It.Is<Stream>(stream => ReferenceEquals(stream, workbookStream)),
                It.Is<IReadOnlyDictionary<string, ExcelSheetLayout>>(layouts => ReferenceEquals(layouts, workbookData.Layouts))))
            .Callback(() => formatCalls++);

        var sut = new MiniExcelReportRenderer(
            Options.Create(settings),
            composerMock.Object,
            fileStoreMock.Object,
            formatterMock.Object,
            CreateReportRunContextAccessor());

        // Act
        sut.RenderReport(rows, statuses, statistics);

        // Assert
        composeCalls.Should().Be(1);
        createWorkbookStreamCalls.Should().Be(1);
        formatCalls.Should().Be(1);
        saveCalls.Should().Be(1);
    }

    private static ExcelWorkbookData CreateWorkbookData()
    {
        var rows = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["C1"] = "Components Version Check",
            },
        };

        var layout = new ExcelSheetLayout("Summary");
        return new ExcelWorkbookData(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["Summary"] = rows,
            },
            new Dictionary<string, ExcelSheetLayout>(StringComparer.Ordinal)
            {
                ["Summary"] = layout,
            });
    }

    private static Dictionary<JiraStatusName, int> CreateStatistics() =>
        new()
        {
            [new JiraStatusName("Done")] = 1,
        };

    private static AppSettings CreateSettings(string outputDirectory, bool excelEnabled) =>
        new()
        {
            DevCsvFilePath = "components.csv",
            TargetCsvFilePath = "components.csv",
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

    private static ComponentCheckRow CreateRow() =>
        new(
            new ComponentCheckIndex(1),
            new ComponentName("component-api"),
            new RepositoryName("repo-api"),
            new VersionLabel("1.0.0"),
            CheckStatus.Outdated,
            new RowDetails("-"),
            []);

    private static string CreateTempDirectoryPath() =>
        Path.Combine(Path.GetTempPath(), $"nrb-excel-tests-{Guid.NewGuid():N}");

    private static IReportRunContextAccessor CreateReportRunContextAccessor(string? excelOutputPathOverride = null)
    {
        var accessorMock = new Mock<IReportRunContextAccessor>(MockBehavior.Loose);
        accessorMock
            .SetupGet(x => x.Current)
            .Returns(new NReleaseBuilder.Models.Rendering.ReportRunContext(
                ExcelOutputPathOverride: excelOutputPathOverride));
        return accessorMock.Object;
    }
}
