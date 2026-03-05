using FluentAssertions;
using System.Globalization;

using Microsoft.Extensions.Options;

using Moq;

using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Presentation.Pdf;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace NReleaseBuilder.Tests.Presentation.Pdf;

public class QuestPdfReportRendererTests
{
    [Fact(DisplayName = "QuestPdfReportRenderer can be created.")]
    [Trait("Category", "Unit")]
    public void QuestPdfReportRendererCanBeCreated()
    {
        // Arrange
        var settings = CreateSettings(pdfEnabled: true);

        var optionsValueReadCount = 0;
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock
            .Setup(x => x.Value)
            .Callback(() => optionsValueReadCount++)
            .Returns(settings);

        var fileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict).Object;
        var contentComposer = new Mock<IPdfContentComposer>(MockBehavior.Strict).Object;
        var reportRunContextAccessor = CreateReportRunContextAccessor();

        // Act
        var exception = Record.Exception(() => _ = new QuestPdfReportRenderer(
            optionsMock.Object,
            fileStore,
            contentComposer,
            reportRunContextAccessor));

        // Assert
        exception.Should().BeNull();
        optionsValueReadCount.Should().Be(1);
    }

    [Fact(DisplayName = "QuestPdfReportRenderer cant be created with null options.")]
    [Trait("Category", "Unit")]
    public void QuestPdfReportRendererCantBeCreatedWithNullOptions()
    {
        // Arrange
        var fileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict).Object;
        var contentComposer = new Mock<IPdfContentComposer>(MockBehavior.Strict).Object;

        // Act
        Action action = () => _ = new QuestPdfReportRenderer(
            null!,
            fileStore,
            contentComposer,
            CreateReportRunContextAccessor());

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact(DisplayName = "QuestPdfReportRenderer cant be created with null file store.")]
    [Trait("Category", "Unit")]
    public void QuestPdfReportRendererCantBeCreatedWithNullFileStore()
    {
        // Arrange
        var options = new Mock<IOptions<AppSettings>>(MockBehavior.Strict).Object;
        var contentComposer = new Mock<IPdfContentComposer>(MockBehavior.Strict).Object;

        // Act
        Action action = () => _ = new QuestPdfReportRenderer(
            options,
            null!,
            contentComposer,
            CreateReportRunContextAccessor());

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("pdfReportFileStore");
    }

    [Fact(DisplayName = "QuestPdfReportRenderer cant be created with null content composer.")]
    [Trait("Category", "Unit")]
    public void QuestPdfReportRendererCantBeCreatedWithNullContentComposer()
    {
        // Arrange
        var options = new Mock<IOptions<AppSettings>>(MockBehavior.Strict).Object;
        var fileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict).Object;

        // Act
        Action action = () => _ = new QuestPdfReportRenderer(
            options,
            fileStore,
            null!,
            CreateReportRunContextAccessor());

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("pdfContentComposer");
    }

    [Fact(DisplayName = "QuestPdfReportRenderer RenderReport validates null arguments.")]
    [Trait("Category", "Unit")]
    public void RenderReportValidatesNullArguments()
    {
        // Arrange
        var settings = CreateSettings(pdfEnabled: false);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var fileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict).Object;
        var contentComposer = new Mock<IPdfContentComposer>(MockBehavior.Strict).Object;

        var sut = new QuestPdfReportRenderer(
            optionsMock.Object,
            fileStore,
            contentComposer,
            CreateReportRunContextAccessor());

        // Act
        Action nullRows = () => sut.RenderReport(null!, [], new Dictionary<JiraStatusName, int>());
        Action nullStatuses = () => sut.RenderReport([], null!, new Dictionary<JiraStatusName, int>());
        Action nullStatistics = () => sut.RenderReport([], [], null!);

        // Assert
        nullRows.Should().Throw<ArgumentNullException>()
            .WithParameterName("rows");
        nullStatuses.Should().Throw<ArgumentNullException>()
            .WithParameterName("allowedStatuses");
        nullStatistics.Should().Throw<ArgumentNullException>()
            .WithParameterName("statusStatistics");
    }

    [Fact(DisplayName = "QuestPdfReportRenderer RenderReport skips rendering when PDF is disabled.")]
    [Trait("Category", "Unit")]
    public void RenderReportSkipsRenderingWhenPdfIsDisabled()
    {
        // Arrange
        var settings = CreateSettings(pdfEnabled: false);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var fileStore = new Mock<IPdfReportFileStore>(MockBehavior.Strict).Object;
        var contentComposer = new Mock<IPdfContentComposer>(MockBehavior.Strict).Object;

        var rows = new[] { CreateRow(1, CheckStatus.UpToDate, []) };
        var statuses = new[] { new JiraStatusName("Done") };
        IReadOnlyDictionary<JiraStatusName, int> statistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("Done")] = 1,
        };

        var sut = new QuestPdfReportRenderer(
            optionsMock.Object,
            fileStore,
            contentComposer,
            CreateReportRunContextAccessor());

        // Act
        var exception = Record.Exception(() => sut.RenderReport(rows, statuses, statistics));

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "QuestPdfReportRenderer RenderReport composes and saves PDF when enabled.")]
    [Trait("Category", "Unit")]
    public void RenderReportComposesAndSavesPdfWhenEnabled()
    {
        // Arrange
        var settings = CreateSettings(pdfEnabled: true, outputPath: Path.Combine("reports", "latest-report.pdf"));

        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var rows =
            new[]
            {
                CreateRow(
                    1,
                    CheckStatus.Outdated,
                    [CreateVersion("2.0.0", "APP-1", "Done")]),
            };
        var statuses = new[] { new JiraStatusName("Done") };
        IReadOnlyDictionary<JiraStatusName, int> statistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("Done")] = 1,
        };

        var dateSuffix = DateTime.Now.ToString("dd_MM_yyyy", CultureInfo.InvariantCulture);
        var expectedOutputPath = Path.GetFullPath(
            Path.Combine("reports", $"latest-report_{dateSuffix}.pdf"),
            Directory.GetCurrentDirectory());

        var composeContentCalls = 0;
        var saveCalls = 0;

        var contentComposerMock = new Mock<IPdfContentComposer>(MockBehavior.Strict);
        contentComposerMock
            .Setup(x => x.ComposeContent(
                It.Is<ColumnDescriptor>(column => column != null),
                It.Is<ComponentCheckRow[]>(inputRows => ReferenceEquals(inputRows, rows)),
                It.Is<JiraStatusName[]>(inputStatuses => ReferenceEquals(inputStatuses, statuses)),
                It.Is<IReadOnlyDictionary<JiraStatusName, int>>(inputStatistics => ReferenceEquals(inputStatistics, statistics))))
            .Callback<ColumnDescriptor, ComponentCheckRow[], JiraStatusName[], IReadOnlyDictionary<JiraStatusName, int>>((column, inputRows, inputStatuses, inputStatistics) => composeContentCalls++);

        var fileStoreMock = new Mock<IPdfReportFileStore>(MockBehavior.Strict);
        fileStoreMock
            .Setup(x => x.Save(
                It.Is<string>(outputPath => string.Equals(outputPath, expectedOutputPath, StringComparison.OrdinalIgnoreCase)),
                It.Is<IDocument>(document => document != null)))
            .Callback<string, IDocument>((outputPath, document) =>
            {
                saveCalls++;
                var pdfBytes = document.GeneratePdf();
                pdfBytes.Should().NotBeEmpty();
            });

        var sut = new QuestPdfReportRenderer(
            optionsMock.Object,
            fileStoreMock.Object,
            contentComposerMock.Object,
            CreateReportRunContextAccessor());

        // Act
        sut.RenderReport(rows, statuses, statistics);

        // Assert
        composeContentCalls.Should().Be(1);
        saveCalls.Should().Be(1);
    }

    private static AppSettings CreateSettings(bool pdfEnabled, string outputPath = "report.pdf") =>
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
            },
            Pdf = new PdfOptions
            {
                Enabled = pdfEnabled,
                OutputPath = outputPath,
            },
        };

    private static ComponentCheckRow CreateRow(
        int index,
        CheckStatus status,
        IReadOnlyList<VersionJiraRow> newerVersions) =>
        new(
            new ComponentCheckIndex(index),
            new ComponentName($"component-{index}"),
            new RepositoryName($"repo-{index}"),
            new VersionLabel("1.0.0"),
            status,
            new RowDetails("-"),
            newerVersions);

    private static VersionJiraRow CreateVersion(string version, string jiraTask, string jiraStatus) =>
        new(
            new VersionLabel(version),
            new JiraTaskReference(jiraTask),
            new JiraTitleReference("Task title"),
            new JiraStatusReference(jiraStatus),
            [
                new JiraTaskAlertDetails(
                    new JiraTaskReference(jiraTask),
                    new JiraTitleReference("Task title"),
                    new JiraStatusReference(jiraStatus),
                    null,
                    null),
            ],
            hasRequiredActions: false,
            hasBreakingChanges: false,
            hasDependencyIssues: false);

    private static IReportRunContextAccessor CreateReportRunContextAccessor(string? pdfOutputPathOverride = null)
    {
        var accessorMock = new Mock<IReportRunContextAccessor>(MockBehavior.Loose);
        accessorMock
            .SetupGet(x => x.Current)
            .Returns(new NReleaseBuilder.Models.Rendering.ReportRunContext(
                PdfOutputPathOverride: pdfOutputPathOverride));
        return accessorMock.Object;
    }
}
