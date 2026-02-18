using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using NReleaseBuilder.Abstractions.Jira;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Jira;
using NReleaseBuilder.Jira.Internal.Models;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Jira;

public class JiraTaskResolverTests
{
    [Fact(DisplayName = "JiraTaskResolver constructor throws when options are null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenOptionsAreNull()
    {
        // Arrange
        var parser = new Mock<IJiraParser>(MockBehavior.Strict).Object;
        var integrationCore = new Mock<IJiraIntegrationCore>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new JiraTaskResolver(null!, parser, integrationCore));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "JiraTaskResolver constructor throws when parser is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenParserIsNull()
    {
        // Arrange
        var options = CreateOptions(checkReleaseAlerts: false);
        var integrationCore = new Mock<IJiraIntegrationCore>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new JiraTaskResolver(options, null!, integrationCore));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "JiraTaskResolver constructor throws when integration core is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenIntegrationCoreIsNull()
    {
        // Arrange
        var options = CreateOptions(checkReleaseAlerts: false);
        var parser = new Mock<IJiraParser>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new JiraTaskResolver(options, parser, null!));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "JiraTaskResolver ResolveFromCommitMessageAsync throws when commit info is null.")]
    [Trait("Category", "Unit")]
    public async Task ResolveFromCommitMessageAsyncThrowsWhenCommitInfoIsNull()
    {
        // Arrange
        var options = CreateOptions(checkReleaseAlerts: false);
        var parser = new Mock<IJiraParser>(MockBehavior.Strict).Object;
        var integrationCore = new Mock<IJiraIntegrationCore>(MockBehavior.Strict).Object;
        using var sut = new JiraTaskResolver(options, parser, integrationCore);
        IReadOnlyList<JiraProjectName> projectNames = [new JiraProjectName("PROJ")];

        // Act
        Func<Task> action = () => sut.ResolveFromCommitMessageAsync(null!, projectNames, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("commitInfo");
    }

    [Fact(DisplayName = "JiraTaskResolver ResolveFromCommitMessageAsync throws when project names are null.")]
    [Trait("Category", "Unit")]
    public async Task ResolveFromCommitMessageAsyncThrowsWhenProjectNamesAreNull()
    {
        // Arrange
        var options = CreateOptions(checkReleaseAlerts: false);
        var parser = new Mock<IJiraParser>(MockBehavior.Strict).Object;
        var integrationCore = new Mock<IJiraIntegrationCore>(MockBehavior.Strict).Object;
        using var sut = new JiraTaskResolver(options, parser, integrationCore);

        // Act
        Func<Task> action = () => sut.ResolveFromCommitMessageAsync(new CommitInfo("PROJ-1"), null!, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("projectNames");
    }

    [Fact(DisplayName = "JiraTaskResolver ResolveFromCommitMessageAsync returns not available when parser returns no tasks.")]
    [Trait("Category", "Unit")]
    public async Task ResolveFromCommitMessageAsyncReturnsNotAvailableWhenParserReturnsNoTasks()
    {
        // Arrange
        var options = CreateOptions(checkReleaseAlerts: false);
        var commitInfo = new CommitInfo("no tasks here");
        IReadOnlyList<JiraProjectName> projectNames = [new JiraProjectName("PROJ")];
        var jiraTask = JiraTaskReference.NotAvailable;
        var extractCount = 0;
        var splitCount = 0;

        var parserMock = new Mock<IJiraParser>(MockBehavior.Strict);
        parserMock
            .Setup(x => x.ExtractJiraTask(
                It.Is<CommitInfo>(value => value.Message == commitInfo.Message),
                It.Is<IReadOnlyList<JiraProjectName>>(value =>
                    value.Count == 1 && value[0].Value == "PROJ")))
            .Callback(() => extractCount++)
            .Returns(jiraTask);
        parserMock
            .Setup(x => x.SplitJiraTasks(
                It.Is<JiraTaskReference>(value => value == jiraTask)))
            .Callback(() => splitCount++)
            .Returns([]);

        var integrationCoreMock = new Mock<IJiraIntegrationCore>(MockBehavior.Strict);
        using var sut = new JiraTaskResolver(options, parserMock.Object, integrationCoreMock.Object);

        // Act
        var result = await sut.ResolveFromCommitMessageAsync(commitInfo, projectNames, CancellationToken.None);

        // Assert
        extractCount.Should().Be(1);
        splitCount.Should().Be(1);
        result.Tasks.Should().Be(jiraTask);
        result.Statuses.Value.Should().Be("N/A");
        result.Titles.Value.Should().Be("N/A");
        result.TaskAlertDetails.Should().BeEmpty();
        result.HasRequiredActions.Should().BeFalse();
        result.HasBreakingChanges.Should().BeFalse();
        result.HasDependencyIssues.Should().BeFalse();
    }

    [Fact(DisplayName = "JiraTaskResolver ResolveFromCommitMessageAsync caches non transient task info across calls.")]
    [Trait("Category", "Unit")]
    public async Task ResolveFromCommitMessageAsyncCachesNonTransientTaskInfoAcrossCalls()
    {
        // Arrange
        var options = CreateOptions(checkReleaseAlerts: false);
        var commitInfo1 = new CommitInfo("PROJ-1 first");
        var commitInfo2 = new CommitInfo("PROJ-1 second");
        IReadOnlyList<JiraProjectName> projectNames = [new JiraProjectName("PROJ")];
        var task = new JiraTaskReference("PROJ-1");
        var taskArray = new[] { task };
        var jiraTaskInfo = new JiraTaskInfo("Done", "Issue title", "Required", "Breaking");
        using var cts = new CancellationTokenSource();
        var integrationCallCount = 0;

        var parserMock = new Mock<IJiraParser>(MockBehavior.Strict);
        parserMock
            .Setup(x => x.ExtractJiraTask(
                It.Is<CommitInfo>(value => value.Message == commitInfo1.Message),
                It.Is<IReadOnlyList<JiraProjectName>>(value =>
                    value.Count == 1 && value[0].Value == "PROJ")))
            .Returns(task);
        parserMock
            .Setup(x => x.ExtractJiraTask(
                It.Is<CommitInfo>(value => value.Message == commitInfo2.Message),
                It.Is<IReadOnlyList<JiraProjectName>>(value =>
                    value.Count == 1 && value[0].Value == "PROJ")))
            .Returns(task);
        parserMock
            .Setup(x => x.SplitJiraTasks(It.Is<JiraTaskReference>(value => value == task)))
            .Returns(taskArray);

        var integrationCoreMock = new Mock<IJiraIntegrationCore>(MockBehavior.Strict);
        integrationCoreMock
            .Setup(x => x.TryGetJiraTaskInfoAsync(
                It.Is<JiraTaskReference>(value => value == task),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => integrationCallCount++)
            .ReturnsAsync(jiraTaskInfo);

        using var sut = new JiraTaskResolver(options, parserMock.Object, integrationCoreMock.Object);

        // Act
        var first = await sut.ResolveFromCommitMessageAsync(commitInfo1, projectNames, cts.Token);
        var second = await sut.ResolveFromCommitMessageAsync(commitInfo2, projectNames, cts.Token);

        // Assert
        integrationCallCount.Should().Be(1);
        first.Statuses.Value.Should().Be("Done");
        second.Statuses.Value.Should().Be("Done");
        first.TaskAlertDetails.Should().HaveCount(1);
        second.TaskAlertDetails.Should().HaveCount(1);
        first.TaskAlertDetails[0].RequiredActionsDetails.Should().BeNull();
        second.TaskAlertDetails[0].BreakingChangesDetails.Should().BeNull();
        first.HasRequiredActions.Should().BeFalse();
        second.HasBreakingChanges.Should().BeFalse();
    }

    [Fact(DisplayName = "JiraTaskResolver ResolveFromCommitMessageAsync does not cache transient task info.")]
    [Trait("Category", "Unit")]
    public async Task ResolveFromCommitMessageAsyncDoesNotCacheTransientTaskInfo()
    {
        // Arrange
        var options = CreateOptions(checkReleaseAlerts: false);
        var commitInfo1 = new CommitInfo("PROJ-1 first");
        var commitInfo2 = new CommitInfo("PROJ-1 second");
        IReadOnlyList<JiraProjectName> projectNames = [new JiraProjectName("PROJ")];
        var task = new JiraTaskReference("PROJ-1");
        var taskArray = new[] { task };
        var transientInfo = JiraTaskInfo.HttpError(System.Net.HttpStatusCode.ServiceUnavailable);
        using var cts = new CancellationTokenSource();
        var integrationCallCount = 0;

        var parserMock = new Mock<IJiraParser>(MockBehavior.Strict);
        parserMock
            .Setup(x => x.ExtractJiraTask(
                It.Is<CommitInfo>(value => value.Message == commitInfo1.Message),
                It.Is<IReadOnlyList<JiraProjectName>>(value =>
                    value.Count == 1 && value[0].Value == "PROJ")))
            .Returns(task);
        parserMock
            .Setup(x => x.ExtractJiraTask(
                It.Is<CommitInfo>(value => value.Message == commitInfo2.Message),
                It.Is<IReadOnlyList<JiraProjectName>>(value =>
                    value.Count == 1 && value[0].Value == "PROJ")))
            .Returns(task);
        parserMock
            .Setup(x => x.SplitJiraTasks(It.Is<JiraTaskReference>(value => value == task)))
            .Returns(taskArray);

        var integrationCoreMock = new Mock<IJiraIntegrationCore>(MockBehavior.Strict);
        integrationCoreMock
            .Setup(x => x.TryGetJiraTaskInfoAsync(
                It.Is<JiraTaskReference>(value => value == task),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => integrationCallCount++)
            .ReturnsAsync(transientInfo);

        using var sut = new JiraTaskResolver(options, parserMock.Object, integrationCoreMock.Object);

        // Act
        _ = await sut.ResolveFromCommitMessageAsync(commitInfo1, projectNames, cts.Token);
        _ = await sut.ResolveFromCommitMessageAsync(commitInfo2, projectNames, cts.Token);

        // Assert
        integrationCallCount.Should().Be(2);
    }

    [Fact(DisplayName = "JiraTaskResolver ResolveFromCommitMessageAsync aggregates release alerts when enabled.")]
    [Trait("Category", "Unit")]
    public async Task ResolveFromCommitMessageAsyncAggregatesReleaseAlertsWhenEnabled()
    {
        // Arrange
        var options = CreateOptions(checkReleaseAlerts: true);
        var commitInfo = new CommitInfo("PROJ-1 PROJ-2");
        IReadOnlyList<JiraProjectName> projectNames = [new JiraProjectName("PROJ")];
        var task1 = new JiraTaskReference("PROJ-1");
        var task2 = new JiraTaskReference("PROJ-2");
        var extractedTask = new JiraTaskReference("PROJ-1, PROJ-2");
        var splitTasks = new[] { task1, task2 };
        var taskInfo1 = new JiraTaskInfo("Done", "Issue one", "Do step A", null);
        var taskInfo2 = new JiraTaskInfo("In Progress", "Issue two", null, "Breaking API");
        using var cts = new CancellationTokenSource();
        var integrationCallCount = 0;
        var hasDependencyIssueCallCount = 0;

        var parserMock = new Mock<IJiraParser>(MockBehavior.Strict);
        parserMock
            .Setup(x => x.ExtractJiraTask(
                It.Is<CommitInfo>(value => value.Message == commitInfo.Message),
                It.Is<IReadOnlyList<JiraProjectName>>(value =>
                    value.Count == 1 && value[0].Value == "PROJ")))
            .Returns(extractedTask);
        parserMock
            .Setup(x => x.SplitJiraTasks(
                It.Is<JiraTaskReference>(value => value == extractedTask)))
            .Returns(splitTasks);
        parserMock
            .Setup(x => x.HasDependencyIssue(
                It.Is<JiraTaskReference>(value => value == task1),
                It.Is<JiraAlertDetails>(value =>
                    value.RequiredActionsDetails == "Do step A" && value.BreakingChangesDetails == null),
                It.Is<IReadOnlyList<JiraProjectName>>(value =>
                    value.Count == 1 && value[0].Value == "PROJ")))
            .Callback(() => hasDependencyIssueCallCount++)
            .Returns(false);
        parserMock
            .Setup(x => x.HasDependencyIssue(
                It.Is<JiraTaskReference>(value => value == task2),
                It.Is<JiraAlertDetails>(value =>
                    value.RequiredActionsDetails == null && value.BreakingChangesDetails == "Breaking API"),
                It.Is<IReadOnlyList<JiraProjectName>>(value =>
                    value.Count == 1 && value[0].Value == "PROJ")))
            .Callback(() => hasDependencyIssueCallCount++)
            .Returns(true);

        var integrationCoreMock = new Mock<IJiraIntegrationCore>(MockBehavior.Strict);
        integrationCoreMock
            .Setup(x => x.TryGetJiraTaskInfoAsync(
                It.Is<JiraTaskReference>(value => value == task1),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => integrationCallCount++)
            .ReturnsAsync(taskInfo1);
        integrationCoreMock
            .Setup(x => x.TryGetJiraTaskInfoAsync(
                It.Is<JiraTaskReference>(value => value == task2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => integrationCallCount++)
            .ReturnsAsync(taskInfo2);

        using var sut = new JiraTaskResolver(options, parserMock.Object, integrationCoreMock.Object);

        // Act
        var result = await sut.ResolveFromCommitMessageAsync(commitInfo, projectNames, cts.Token);

        // Assert
        integrationCallCount.Should().Be(2);
        hasDependencyIssueCallCount.Should().Be(2);
        result.Statuses.Value.Should().Be("Done, In Progress");
        result.Tasks.Value.Should().Be("PROJ-1, PROJ-2");
        result.Titles.Value.Should().Be("Issue one, Issue two");
        result.TaskAlertDetails.Should().HaveCount(2);
        result.TaskAlertDetails[0].RequiredActionsDetails.Should().Be("Do step A");
        result.TaskAlertDetails[1].BreakingChangesDetails.Should().Be("Breaking API");
        result.HasRequiredActions.Should().BeTrue();
        result.HasBreakingChanges.Should().BeTrue();
        result.HasDependencyIssues.Should().BeTrue();
    }

    [Fact(DisplayName = "JiraTaskResolver ResolveFromCommitMessageAsync ignores release alerts when disabled.")]
    [Trait("Category", "Unit")]
    public async Task ResolveFromCommitMessageAsyncIgnoresReleaseAlertsWhenDisabled()
    {
        // Arrange
        var options = CreateOptions(checkReleaseAlerts: false);
        var commitInfo = new CommitInfo("PROJ-1");
        IReadOnlyList<JiraProjectName> projectNames = [new JiraProjectName("PROJ")];
        var task = new JiraTaskReference("PROJ-1");
        var splitTasks = new[] { task };
        var extractedTask = new JiraTaskReference("PROJ-1");
        var taskInfo = new JiraTaskInfo("Done", "Issue title", "Required actions", "Breaking changes");
        using var cts = new CancellationTokenSource();

        var parserMock = new Mock<IJiraParser>(MockBehavior.Strict);
        parserMock
            .Setup(x => x.ExtractJiraTask(
                It.Is<CommitInfo>(value => value.Message == commitInfo.Message),
                It.Is<IReadOnlyList<JiraProjectName>>(value =>
                    value.Count == 1 && value[0].Value == "PROJ")))
            .Returns(extractedTask);
        parserMock
            .Setup(x => x.SplitJiraTasks(
                It.Is<JiraTaskReference>(value => value == extractedTask)))
            .Returns(splitTasks);

        var integrationCoreMock = new Mock<IJiraIntegrationCore>(MockBehavior.Strict);
        integrationCoreMock
            .Setup(x => x.TryGetJiraTaskInfoAsync(
                It.Is<JiraTaskReference>(value => value == task),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(taskInfo);

        using var sut = new JiraTaskResolver(options, parserMock.Object, integrationCoreMock.Object);

        // Act
        var result = await sut.ResolveFromCommitMessageAsync(commitInfo, projectNames, cts.Token);

        // Assert
        result.TaskAlertDetails.Should().HaveCount(1);
        result.TaskAlertDetails[0].RequiredActionsDetails.Should().BeNull();
        result.TaskAlertDetails[0].BreakingChangesDetails.Should().BeNull();
        result.HasRequiredActions.Should().BeFalse();
        result.HasBreakingChanges.Should().BeFalse();
        result.HasDependencyIssues.Should().BeFalse();
    }

    private static IOptions<AppSettings> CreateOptions(bool checkReleaseAlerts)
    {
        var settings = new AppSettings
        {
            CsvFilePath = "components.csv",
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
                CheckReleaseAlerts = checkReleaseAlerts,
                MaxParallelRequests = 2,
            },
        };

        return Options.Create(settings);
    }
}
