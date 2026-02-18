using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Presentation.Console;

namespace NReleaseBuilder.Tests.Presentation.Console;

public class SpectreConsoleOutputRendererTests
{
    [Fact(DisplayName = "SpectreConsoleOutputRenderer can be created.")]
    [Trait("Category", "Unit")]
    public void SpectreConsoleOutputRendererCanBeCreated()
    {
        // Arrange
        var settings = CreateSettings();
        var valueReadCount = 0;
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock
            .Setup(x => x.Value)
            .Callback(() => valueReadCount++)
            .Returns(settings);

        // Act
        var exception = Record.Exception(() => new SpectreConsoleOutputRenderer(optionsMock.Object));

        // Assert
        exception.Should().BeNull();
        valueReadCount.Should().Be(1);
    }

    [Fact(DisplayName = "SpectreConsoleOutputRenderer cant be created with null options.")]
    [Trait("Category", "Unit")]
    public void SpectreConsoleOutputRendererCantBeCreatedWithNullOptions()
    {
        // Arrange
        // Act
        Action action = () => _ = new SpectreConsoleOutputRenderer(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact(DisplayName = "SpectreConsoleOutputRenderer methods can render valid input without throwing.")]
    [Trait("Category", "Unit")]
    public void SpectreConsoleOutputRendererMethodsCanRenderValidInputWithoutThrowing()
    {
        // Arrange
        var settings = CreateSettings(["Done", "In Progress"]);
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var rows =
            new[]
            {
                CreateRow(1, CheckStatus.UpToDate, "Already up to date", []),
                CreateRow(
                    2,
                    CheckStatus.Outdated,
                    "Has newer versions",
                    [CreateVersion("2.0.0", "APP-1", "Done", hasRequiredActions: true, hasBreakingChanges: false)]),
            };
        var statuses = new[] { new JiraStatusName("Done") };
        var statusStatistics = new Dictionary<JiraStatusName, int>
        {
            [new JiraStatusName("In Progress")] = 2,
            [new JiraStatusName("Done")] = 1,
        };
        var sut = new SpectreConsoleOutputRenderer(optionsMock.Object);

        // Act
        var exception = Record.Exception(() =>
        {
            sut.RenderHeader();
            sut.PrintRepositoryCheckCount(2);
            sut.PrintRepositoryBatchProgress(1, 2, 0, 2, 4);
            sut.PrintNoRows();
            sut.PrintNoComponentsMatchedStatusFilter(statuses);
            sut.PrintStatusFilterDiagnostics(statusStatistics, statuses);
            sut.RenderTable(rows);
            sut.RenderSummary(rows);
            sut.RenderUniqueJiraTaskStatusChart(rows);
            sut.PrintError(new ErrorMessage("boom"));
        });

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "SpectreConsoleOutputRenderer PrintRepositoryBatchProgress validates argument ranges.")]
    [Trait("Category", "Unit")]
    public void PrintRepositoryBatchProgressValidatesArgumentRanges()
    {
        // Arrange
        var settings = CreateSettings();
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var sut = new SpectreConsoleOutputRenderer(optionsMock.Object);

        // Act
        Action batchNumberAction = () => sut.PrintRepositoryBatchProgress(0, 1, 0, 1, 1);
        Action totalBatchesAction = () => sut.PrintRepositoryBatchProgress(1, 0, 0, 1, 1);
        Action processedAction = () => sut.PrintRepositoryBatchProgress(1, 1, -1, 1, 1);
        Action currentBatchAction = () => sut.PrintRepositoryBatchProgress(1, 1, 0, 0, 1);
        Action totalRepositoriesAction = () => sut.PrintRepositoryBatchProgress(1, 1, 0, 1, 0);

        // Assert
        batchNumberAction.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("batchNumber");
        totalBatchesAction.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("totalBatchCount");
        processedAction.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("processedRepositoryCount");
        currentBatchAction.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("currentBatchRepositoryCount");
        totalRepositoriesAction.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("totalRepositoryCount");
    }

    [Fact(DisplayName = "SpectreConsoleOutputRenderer RunBitbucketLoadingWithProgressAsync returns operation result.")]
    [Trait("Category", "Unit")]
    public async Task RunBitbucketLoadingWithProgressAsyncReturnsOperationResult()
    {
        // Arrange
        var settings = CreateSettings();
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var repositories = new[]
        {
            new RepositoryName("repo-api"),
            new RepositoryName("repo-api"),
            new RepositoryName("repo-gateway"),
        };

        var operationCallCount = 0;
        var startedCallbacks = 0;
        var totalDetectedCallbacks = 0;
        var processedCallbacks = 0;
        var completedCallbacks = 0;

        var sut = new SpectreConsoleOutputRenderer(optionsMock.Object);

        // Act
        var result = await sut.RunBitbucketLoadingWithProgressAsync(
            repositories,
            callbacks =>
            {
                operationCallCount++;

                callbacks.RepositoryStarted!("repo-api");
                startedCallbacks++;

                callbacks.CommitTotalDetected!("repo-api", 2);
                totalDetectedCallbacks++;

                callbacks.CommitProcessed!("repo-api");
                callbacks.CommitProcessed!("repo-api");
                processedCallbacks += 2;

                callbacks.RepositoryCompleted!("repo-api");
                completedCallbacks++;

                return Task.FromResult(73);
            });

        // Assert
        result.Should().Be(73);
        operationCallCount.Should().Be(1);
        startedCallbacks.Should().Be(1);
        totalDetectedCallbacks.Should().Be(1);
        processedCallbacks.Should().Be(2);
        completedCallbacks.Should().Be(1);
    }

    [Fact(DisplayName = "SpectreConsoleOutputRenderer RunBitbucketLoadingWithProgressAsync validates null inputs.")]
    [Trait("Category", "Unit")]
    public async Task RunBitbucketLoadingWithProgressAsyncValidatesNullInputs()
    {
        // Arrange
        var settings = CreateSettings();
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var repositories = new[] { new RepositoryName("repo-api") };
        var sut = new SpectreConsoleOutputRenderer(optionsMock.Object);

        // Act
        Func<Task> nullRepositories = async () => await sut.RunBitbucketLoadingWithProgressAsync<int>(null!, callbacks => Task.FromResult(1));
        Func<Task> nullOperation = async () => await sut.RunBitbucketLoadingWithProgressAsync<int>(repositories, null!);

        // Assert
        await nullRepositories.Should().ThrowAsync<ArgumentNullException>();
        await nullOperation.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "SpectreConsoleOutputRenderer RunBitbucketLoadingWithProgressAsync throws when operation returns null result.")]
    [Trait("Category", "Unit")]
    public async Task RunBitbucketLoadingWithProgressAsyncThrowsWhenOperationReturnsNullResult()
    {
        // Arrange
        var settings = CreateSettings();
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var repositories = new[] { new RepositoryName("repo-api") };
        var sut = new SpectreConsoleOutputRenderer(optionsMock.Object);

        // Act
        Func<Task> action = async () => await sut.RunBitbucketLoadingWithProgressAsync(
            repositories,
            callbacks => Task.FromResult<string>(null!));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Operation completed without returning a result.");
    }

    [Fact(DisplayName = "SpectreConsoleOutputRenderer validates null collections for status and row methods.")]
    [Trait("Category", "Unit")]
    public void SpectreConsoleOutputRendererValidatesNullCollectionsForStatusAndRowMethods()
    {
        // Arrange
        var settings = CreateSettings();
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var sut = new SpectreConsoleOutputRenderer(optionsMock.Object);

        // Act
        Action printNoComponents = () => sut.PrintNoComponentsMatchedStatusFilter(null!);
        Action printDiagnosticsStatusMap = () => sut.PrintStatusFilterDiagnostics(null!, []);
        Action printDiagnosticsAllowedStatuses = () => sut.PrintStatusFilterDiagnostics(new Dictionary<JiraStatusName, int>(), null!);
        Action renderTable = () => sut.RenderTable(null!);
        Action renderSummary = () => sut.RenderSummary(null!);
        Action renderChart = () => sut.RenderUniqueJiraTaskStatusChart(null!);

        // Assert
        printNoComponents.Should().Throw<ArgumentNullException>()
            .WithParameterName("statuses");
        printDiagnosticsStatusMap.Should().Throw<ArgumentNullException>()
            .WithParameterName("statusStatistics");
        printDiagnosticsAllowedStatuses.Should().Throw<ArgumentNullException>()
            .WithParameterName("allowedStatuses");
        renderTable.Should().Throw<ArgumentNullException>()
            .WithParameterName("rows");
        renderSummary.Should().Throw<ArgumentNullException>()
            .WithParameterName("rows");
        renderChart.Should().Throw<ArgumentNullException>()
            .WithParameterName("rows");
    }

    [Fact(DisplayName = "SpectreConsoleOutputRenderer PrintError throws for empty message value.")]
    [Trait("Category", "Unit")]
    public void PrintErrorThrowsForEmptyMessageValue()
    {
        // Arrange
        var settings = CreateSettings();
        var optionsMock = new Mock<IOptions<AppSettings>>(MockBehavior.Strict);
        optionsMock.Setup(x => x.Value).Returns(settings);

        var sut = new SpectreConsoleOutputRenderer(optionsMock.Object);

        // Act
        Action action = () => sut.PrintError(default);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("message");
    }

    private static AppSettings CreateSettings(IReadOnlyList<string>? allowedTaskStatuses = null) =>
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
                AllowedTaskStatuses = allowedTaskStatuses ?? [],
            },
            Pdf = new PdfOptions
            {
                Enabled = true,
                OutputPath = "report.pdf",
            },
        };

    private static ComponentCheckRow CreateRow(
        int index,
        CheckStatus status,
        string details,
        IReadOnlyList<VersionJiraRow> newerVersions) =>
        new(
            new ComponentCheckIndex(index),
            new ComponentName($"component-{index}"),
            new RepositoryName($"repo-{index}"),
            new VersionLabel("1.0.0"),
            status,
            new RowDetails(details),
            newerVersions);

    private static VersionJiraRow CreateVersion(
        string version,
        string jiraTask,
        string jiraStatus,
        bool hasRequiredActions,
        bool hasBreakingChanges) =>
        new(
            new VersionLabel(version),
            new JiraTaskReference(jiraTask),
            new JiraTitleReference("Task title"),
            new JiraStatusReference(jiraStatus),
            [
                new JiraTaskAlertDetails(
                    new JiraTaskReference("APP-1"),
                    new JiraTitleReference("Task title"),
                    new JiraStatusReference(jiraStatus),
                    hasRequiredActions ? "Required details" : null,
                    hasBreakingChanges ? "Breaking details" : null),
            ],
            hasRequiredActions,
            hasBreakingChanges,
            hasDependencyIssues: false);
}
