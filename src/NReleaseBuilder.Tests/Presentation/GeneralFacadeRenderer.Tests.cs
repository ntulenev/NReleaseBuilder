using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using NReleaseBuilder.Abstractions.Jira;
using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Presentation;

namespace NReleaseBuilder.Tests.Presentation;

public class GeneralFacadeRendererTests
{
    [Fact(DisplayName = "GeneralFacadeRenderer can be created.")]
    [Trait("Category", "Unit")]
    public void GeneralFacadeRendererCanBeCreated()
    {
        // Arrange
        var consoleRenderer = new Mock<IConsoleOutputRenderer>(MockBehavior.Strict).Object;
        var pdfRenderer = new Mock<IPdfReportRenderer>(MockBehavior.Strict).Object;
        var statusStatisticsBuilder = new Mock<IJiraStatusStatisticsBuilder>(MockBehavior.Strict).Object;
        var settings = CreateSettings();

        var optionsValueReadCount = 0;
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock
            .Setup(x => x.Value)
            .Callback(() => optionsValueReadCount++)
            .Returns(settings);

        // Act
        var exception = Record.Exception(() =>
            new GeneralFacadeRenderer(consoleRenderer, pdfRenderer, statusStatisticsBuilder, optionsMock.Object));

        // Assert
        exception.Should().BeNull();
        optionsValueReadCount.Should().Be(1);
    }

    [Fact(DisplayName = "GeneralFacadeRenderer cant be created with null console renderer.")]
    [Trait("Category", "Unit")]
    public void GeneralFacadeRendererCantBeCreatedWithNullConsoleRenderer()
    {
        // Arrange
        var pdfRenderer = new Mock<IPdfReportRenderer>(MockBehavior.Strict).Object;
        var statusStatisticsBuilder = new Mock<IJiraStatusStatisticsBuilder>(MockBehavior.Strict).Object;
        var options = new Mock<IOptions<AppSettings>>(MockBehavior.Strict).Object;

        // Act
        Action action = () => _ = new GeneralFacadeRenderer(null!, pdfRenderer, statusStatisticsBuilder, options);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("consoleRenderer");
    }

    [Fact(DisplayName = "GeneralFacadeRenderer cant be created with null pdf renderer.")]
    [Trait("Category", "Unit")]
    public void GeneralFacadeRendererCantBeCreatedWithNullPdfRenderer()
    {
        // Arrange
        var consoleRenderer = new Mock<IConsoleOutputRenderer>(MockBehavior.Strict).Object;
        var statusStatisticsBuilder = new Mock<IJiraStatusStatisticsBuilder>(MockBehavior.Strict).Object;
        var options = new Mock<IOptions<AppSettings>>(MockBehavior.Strict).Object;

        // Act
        Action action = () => _ = new GeneralFacadeRenderer(consoleRenderer, null!, statusStatisticsBuilder, options);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("pdfReportRenderer");
    }

    [Fact(DisplayName = "GeneralFacadeRenderer cant be created with null jira status statistics builder.")]
    [Trait("Category", "Unit")]
    public void GeneralFacadeRendererCantBeCreatedWithNullJiraStatusStatisticsBuilder()
    {
        // Arrange
        var consoleRenderer = new Mock<IConsoleOutputRenderer>(MockBehavior.Strict).Object;
        var pdfRenderer = new Mock<IPdfReportRenderer>(MockBehavior.Strict).Object;
        var options = new Mock<IOptions<AppSettings>>(MockBehavior.Strict).Object;

        // Act
        Action action = () => _ = new GeneralFacadeRenderer(consoleRenderer, pdfRenderer, null!, options);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("jiraStatusStatisticsBuilder");
    }

    [Fact(DisplayName = "GeneralFacadeRenderer cant be created with null options.")]
    [Trait("Category", "Unit")]
    public void GeneralFacadeRendererCantBeCreatedWithNullOptions()
    {
        // Arrange
        var consoleRenderer = new Mock<IConsoleOutputRenderer>(MockBehavior.Strict).Object;
        var pdfRenderer = new Mock<IPdfReportRenderer>(MockBehavior.Strict).Object;
        var statusStatisticsBuilder = new Mock<IJiraStatusStatisticsBuilder>(MockBehavior.Strict).Object;

        // Act
        Action action = () => _ = new GeneralFacadeRenderer(consoleRenderer, pdfRenderer, statusStatisticsBuilder, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact(DisplayName = "GeneralFacadeRenderer RenderResults throws when rows are null.")]
    [Trait("Category", "Unit")]
    public void RenderResultsThrowsWhenRowsAreNull()
    {
        // Arrange
        var settings = CreateSettings(["Done"]);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var consoleRenderer = new Mock<IConsoleOutputRenderer>(MockBehavior.Strict).Object;
        var pdfRenderer = new Mock<IPdfReportRenderer>(MockBehavior.Strict).Object;
        var statusStatisticsBuilder = new Mock<IJiraStatusStatisticsBuilder>(MockBehavior.Strict).Object;

        var sut = new GeneralFacadeRenderer(consoleRenderer, pdfRenderer, statusStatisticsBuilder, optionsMock.Object);

        // Act
        Action action = () => sut.RenderResults(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("rows");
    }

    [Fact(DisplayName = "GeneralFacadeRenderer delegates non-result rendering methods to console renderer.")]
    [Trait("Category", "Unit")]
    public async Task DelegatesNonResultRenderingMethodsToConsoleRenderer()
    {
        // Arrange
        var settings = CreateSettings();
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var renderHeaderCount = 0;
        var repositoryCountCalls = 0;
        var batchProgressCalls = 0;
        var runWithProgressCalls = 0;
        var noRowsCalls = 0;
        var printNoComponentsCalls = 0;
        var printStatusDiagnosticsCalls = 0;
        var renderTableCalls = 0;
        var renderSummaryCalls = 0;
        var printErrorCalls = 0;

        var expectedRows =
            new[] { CreateRow(1, "api", "repo-api", "1.0.0", CheckStatus.UpToDate, "-") };
        var expectedStatuses = new[] { new JiraStatusName("Done") };
        var expectedStatistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("Done")] = 1,
        };
        var repositories = new[] { new RepositoryName("repo-api") };

        var consoleRendererMock = new Mock<IConsoleOutputRenderer>(MockBehavior.Strict);
        consoleRendererMock
            .Setup(x => x.RenderHeader())
            .Callback(() => renderHeaderCount++);
        consoleRendererMock
            .Setup(x => x.PrintRepositoryCheckCount(It.Is<int>(count => count == 2)))
            .Callback<int>(count => repositoryCountCalls++);
        consoleRendererMock
            .Setup(x => x.PrintRepositoryBatchProgress(
                It.Is<int>(batchNumber => batchNumber == 1),
                It.Is<int>(totalBatchCount => totalBatchCount == 2),
                It.Is<int>(processedRepositoryCount => processedRepositoryCount == 3),
                It.Is<int>(currentBatchRepositoryCount => currentBatchRepositoryCount == 4),
                It.Is<int>(totalRepositoryCount => totalRepositoryCount == 5)))
            .Callback<int, int, int, int, int>((batchNumber, totalBatchCount, processedRepositoryCount, currentBatchRepositoryCount, totalRepositoryCount) => batchProgressCalls++);
        consoleRendererMock
            .Setup(x => x.RunBitbucketLoadingWithProgressAsync(
                It.Is<IReadOnlyList<RepositoryName>>(input => input.Count == 1 && input[0] == repositories[0]),
                It.Is<Func<BitbucketProgressCallbacks, Task<int>>>(operation => operation != null)))
            .Callback<IReadOnlyList<RepositoryName>, Func<BitbucketProgressCallbacks, Task<int>>>((input, operation) => runWithProgressCalls++)
            .Returns<IReadOnlyList<RepositoryName>, Func<BitbucketProgressCallbacks, Task<int>>>((input, operation) => operation(new BitbucketProgressCallbacks()));
        consoleRendererMock
            .Setup(x => x.PrintNoRows())
            .Callback(() => noRowsCalls++);
        consoleRendererMock
            .Setup(x => x.PrintNoComponentsMatchedStatusFilter(
                It.Is<IReadOnlyList<JiraStatusName>>(statuses => statuses.Count == 1 && statuses[0] == expectedStatuses[0])))
            .Callback<IReadOnlyList<JiraStatusName>>(statuses => printNoComponentsCalls++);
        consoleRendererMock
            .Setup(x => x.PrintStatusFilterDiagnostics(
                It.Is<IReadOnlyDictionary<JiraStatusName, int>>(statistics =>
                    statistics.Count == 1
                    && statistics[new JiraStatusName("Done")] == 1),
                It.Is<IReadOnlyList<JiraStatusName>>(statuses => statuses.Count == 1 && statuses[0] == expectedStatuses[0])))
            .Callback<IReadOnlyDictionary<JiraStatusName, int>, IReadOnlyList<JiraStatusName>>((statistics, statuses) => printStatusDiagnosticsCalls++);
        consoleRendererMock
            .Setup(x => x.RenderTable(It.Is<IReadOnlyList<ComponentCheckRow>>(rows => rows.Count == 1 && rows[0] == expectedRows[0])))
            .Callback<IReadOnlyList<ComponentCheckRow>>(rows => renderTableCalls++);
        consoleRendererMock
            .Setup(x => x.RenderSummary(It.Is<IReadOnlyList<ComponentCheckRow>>(rows => rows.Count == 1 && rows[0] == expectedRows[0])))
            .Callback<IReadOnlyList<ComponentCheckRow>>(rows => renderSummaryCalls++);
        consoleRendererMock
            .Setup(x => x.PrintError(It.Is<ErrorMessage>(message => message.Value == "oops")))
            .Callback<ErrorMessage>(message => printErrorCalls++);

        var pdfRenderer = new Mock<IPdfReportRenderer>(MockBehavior.Strict).Object;
        var statusStatisticsBuilder = new Mock<IJiraStatusStatisticsBuilder>(MockBehavior.Strict).Object;

        var sut = new GeneralFacadeRenderer(consoleRendererMock.Object, pdfRenderer, statusStatisticsBuilder, optionsMock.Object);

        // Act
        sut.RenderHeader();
        sut.PrintRepositoryCheckCount(2);
        sut.PrintRepositoryBatchProgress(1, 2, 3, 4, 5);
        var runResult = await sut.RunBitbucketLoadingWithProgressAsync(
            repositories,
            callbacks => Task.FromResult(42));
        sut.PrintNoRows();
        sut.PrintNoComponentsMatchedStatusFilter(expectedStatuses);
        sut.PrintStatusFilterDiagnostics(expectedStatistics, expectedStatuses);
        sut.RenderTable(expectedRows);
        sut.RenderSummary(expectedRows);
        sut.PrintError(new ErrorMessage("oops"));

        // Assert
        runResult.Should().Be(42);
        renderHeaderCount.Should().Be(1);
        repositoryCountCalls.Should().Be(1);
        batchProgressCalls.Should().Be(1);
        runWithProgressCalls.Should().Be(1);
        noRowsCalls.Should().Be(1);
        printNoComponentsCalls.Should().Be(1);
        printStatusDiagnosticsCalls.Should().Be(1);
        renderTableCalls.Should().Be(1);
        renderSummaryCalls.Should().Be(1);
        printErrorCalls.Should().Be(1);
    }

    [Fact(DisplayName = "GeneralFacadeRenderer RenderResults prints empty-state diagnostics when no rows match allowed statuses.")]
    [Trait("Category", "Unit")]
    public void RenderResultsPrintsEmptyStateDiagnosticsWhenNoRowsMatchAllowedStatuses()
    {
        // Arrange
        var allowedStatuses = new[] { new JiraStatusName("Done") };
        var rows =
            new[] { CreateOutdatedRow(1, "api", "repo-api", "1.0.0", "APP-1", "In Progress") };
        var statistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("In Progress")] = 1,
        };

        var settings = CreateSettings(["Done"]);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var buildCalls = 0;
        var printNoComponentsCalls = 0;
        var printStatusDiagnosticsCalls = 0;
        var renderPdfCalls = 0;

        var consoleRendererMock = new Mock<IConsoleOutputRenderer>(MockBehavior.Strict);
        consoleRendererMock
            .Setup(x => x.PrintNoComponentsMatchedStatusFilter(
                It.Is<IReadOnlyList<JiraStatusName>>(statuses =>
                    statuses.Count == 1
                    && statuses[0] == allowedStatuses[0])))
            .Callback<IReadOnlyList<JiraStatusName>>(statuses => printNoComponentsCalls++);
        consoleRendererMock
            .Setup(x => x.PrintStatusFilterDiagnostics(
                It.Is<IReadOnlyDictionary<JiraStatusName, int>>(statusStatistics =>
                    statusStatistics.Count == 1
                    && statusStatistics[new JiraStatusName("In Progress")] == 1),
                It.Is<IReadOnlyList<JiraStatusName>>(statuses =>
                    statuses.Count == 1
                    && statuses[0] == allowedStatuses[0])))
            .Callback<IReadOnlyDictionary<JiraStatusName, int>, IReadOnlyList<JiraStatusName>>((statusStatistics, statuses) => printStatusDiagnosticsCalls++);

        var pdfRendererMock = new Mock<IPdfReportRenderer>(MockBehavior.Strict);
        pdfRendererMock
            .Setup(x => x.RenderReport(
                It.Is<ComponentCheckRow[]>(filteredRows => filteredRows.Length == 0),
                It.Is<JiraStatusName[]>(statuses => statuses.Length == 1 && statuses[0] == allowedStatuses[0]),
                It.Is<IReadOnlyDictionary<JiraStatusName, int>>(statusStatistics => ReferenceEquals(statusStatistics, statistics))))
            .Callback<ComponentCheckRow[], JiraStatusName[], IReadOnlyDictionary<JiraStatusName, int>>((filteredRows, statuses, statusStatistics) => renderPdfCalls++);

        var statisticsBuilderMock = new Mock<IJiraStatusStatisticsBuilder>(MockBehavior.Strict);
        statisticsBuilderMock
            .Setup(x => x.Build(It.Is<IReadOnlyList<ComponentCheckRow>>(inputRows => ReferenceEquals(inputRows, rows))))
            .Callback<IReadOnlyList<ComponentCheckRow>>(inputRows => buildCalls++)
            .Returns(statistics);

        var sut = new GeneralFacadeRenderer(
            consoleRendererMock.Object,
            pdfRendererMock.Object,
            statisticsBuilderMock.Object,
            optionsMock.Object);

        // Act
        sut.RenderResults(rows);

        // Assert
        buildCalls.Should().Be(1);
        printNoComponentsCalls.Should().Be(1);
        printStatusDiagnosticsCalls.Should().Be(1);
        renderPdfCalls.Should().Be(1);
    }

    [Fact(DisplayName = "GeneralFacadeRenderer RenderResults renders table summary chart and pdf when rows match allowed statuses.")]
    [Trait("Category", "Unit")]
    public void RenderResultsRendersTableSummaryChartAndPdfWhenRowsMatchAllowedStatuses()
    {
        // Arrange
        var allowedStatuses = new[] { new JiraStatusName("Done") };
        var rows =
            new[] { CreateOutdatedRow(1, "api", "repo-api", "1.0.0", "APP-1", "Done") };
        var statistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("Done")] = 1,
        };

        var settings = CreateSettings(["Done"]);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var buildCalls = 0;
        var renderTableCalls = 0;
        var renderSummaryCalls = 0;
        var renderChartCalls = 0;
        var renderPdfCalls = 0;

        var consoleRendererMock = new Mock<IConsoleOutputRenderer>(MockBehavior.Strict);
        consoleRendererMock
            .Setup(x => x.RenderTable(
                It.Is<IReadOnlyList<ComponentCheckRow>>(filteredRows =>
                    filteredRows.Count == 1
                    && filteredRows[0] == rows[0])))
            .Callback<IReadOnlyList<ComponentCheckRow>>(filteredRows => renderTableCalls++);
        consoleRendererMock
            .Setup(x => x.RenderSummary(
                It.Is<IReadOnlyList<ComponentCheckRow>>(filteredRows =>
                    filteredRows.Count == 1
                    && filteredRows[0] == rows[0])))
            .Callback<IReadOnlyList<ComponentCheckRow>>(filteredRows => renderSummaryCalls++);
        consoleRendererMock
            .Setup(x => x.RenderUniqueJiraTaskStatusChart(
                It.Is<IReadOnlyList<ComponentCheckRow>>(filteredRows =>
                    filteredRows.Count == 1
                    && filteredRows[0] == rows[0])))
            .Callback<IReadOnlyList<ComponentCheckRow>>(filteredRows => renderChartCalls++);

        var pdfRendererMock = new Mock<IPdfReportRenderer>(MockBehavior.Strict);
        pdfRendererMock
            .Setup(x => x.RenderReport(
                It.Is<ComponentCheckRow[]>(filteredRows => filteredRows.Length == 1 && filteredRows[0] == rows[0]),
                It.Is<JiraStatusName[]>(statuses => statuses.Length == 1 && statuses[0] == allowedStatuses[0]),
                It.Is<IReadOnlyDictionary<JiraStatusName, int>>(statusStatistics => ReferenceEquals(statusStatistics, statistics))))
            .Callback<ComponentCheckRow[], JiraStatusName[], IReadOnlyDictionary<JiraStatusName, int>>((filteredRows, statuses, statusStatistics) => renderPdfCalls++);

        var statisticsBuilderMock = new Mock<IJiraStatusStatisticsBuilder>(MockBehavior.Strict);
        statisticsBuilderMock
            .Setup(x => x.Build(It.Is<IReadOnlyList<ComponentCheckRow>>(inputRows => ReferenceEquals(inputRows, rows))))
            .Callback<IReadOnlyList<ComponentCheckRow>>(inputRows => buildCalls++)
            .Returns(statistics);

        var sut = new GeneralFacadeRenderer(
            consoleRendererMock.Object,
            pdfRendererMock.Object,
            statisticsBuilderMock.Object,
            optionsMock.Object);

        // Act
        sut.RenderResults(rows);

        // Assert
        buildCalls.Should().Be(1);
        renderTableCalls.Should().Be(1);
        renderSummaryCalls.Should().Be(1);
        renderChartCalls.Should().Be(1);
        renderPdfCalls.Should().Be(1);
    }

    private static AppSettings CreateSettings(IReadOnlyList<string>? allowedStatuses = null) =>
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
                AllowedTaskStatuses = allowedStatuses ?? [],
                RequiredActionsFieldName = "Required Actions",
                BreakingChangesFieldName = "Breaking changes",
            },
            Pdf = new PdfOptions
            {
                Enabled = true,
                OutputPath = "report.pdf",
            },
        };

    private static ComponentCheckRow CreateRow(
        int index,
        string component,
        string repository,
        string version,
        CheckStatus status,
        string details,
        IReadOnlyList<VersionJiraRow>? newerVersions = null) =>
        new(
            new ComponentCheckIndex(index),
            new ComponentName(component),
            new RepositoryName(repository),
            new VersionLabel(version),
            status,
            new RowDetails(details),
            newerVersions ?? []);

    private static ComponentCheckRow CreateOutdatedRow(
        int index,
        string component,
        string repository,
        string currentVersion,
        string jiraTask,
        string jiraStatus)
    {
        var version = new VersionJiraRow(
            new VersionLabel("2.0.0"),
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

        return CreateRow(
            index,
            component,
            repository,
            currentVersion,
            CheckStatus.Outdated,
            "Has newer version",
            [version]);
    }
}
