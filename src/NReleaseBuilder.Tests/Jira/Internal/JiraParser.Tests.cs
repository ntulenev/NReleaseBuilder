using FluentAssertions;

using NReleaseBuilder.Jira.Internal;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Jira.Internal;

public class JiraParserTests
{
    [Fact(DisplayName = "JiraParser ExtractJiraTask throws when commit info is null.")]
    [Trait("Category", "Unit")]
    public void ExtractJiraTaskThrowsWhenCommitInfoIsNull()
    {
        // Arrange
        var sut = new JiraParser();
        IReadOnlyList<JiraProjectName> projectNames = [new JiraProjectName("PROJ")];

        // Act
        Action action = () => _ = sut.ExtractJiraTask(null!, projectNames);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("commitInfo");
    }

    [Fact(DisplayName = "JiraParser ExtractJiraTask throws when project names are null.")]
    [Trait("Category", "Unit")]
    public void ExtractJiraTaskThrowsWhenProjectNamesAreNull()
    {
        // Arrange
        var sut = new JiraParser();
        var commitInfo = new CommitInfo("PROJ-1 update");

        // Act
        Action action = () => _ = sut.ExtractJiraTask(commitInfo, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("projectNames");
    }

    [Fact(DisplayName = "JiraParser ExtractJiraTask returns not available for empty commit message.")]
    [Trait("Category", "Unit")]
    public void ExtractJiraTaskReturnsNotAvailableForEmptyCommitMessage()
    {
        // Arrange
        var sut = new JiraParser();
        var commitInfo = new CommitInfo(" ");
        IReadOnlyList<JiraProjectName> projectNames = [new JiraProjectName("PROJ")];

        // Act
        var result = sut.ExtractJiraTask(commitInfo, projectNames);

        // Assert
        result.Should().Be(JiraTaskReference.NotAvailable);
    }

    [Fact(DisplayName = "JiraParser ExtractJiraTask returns not available when no projects are provided.")]
    [Trait("Category", "Unit")]
    public void ExtractJiraTaskReturnsNotAvailableWhenNoProjectsAreProvided()
    {
        // Arrange
        var sut = new JiraParser();
        var commitInfo = new CommitInfo("PROJ-1 update");
        IReadOnlyList<JiraProjectName> projectNames = [];

        // Act
        var result = sut.ExtractJiraTask(commitInfo, projectNames);

        // Assert
        result.Should().Be(JiraTaskReference.NotAvailable);
    }

    [Fact(DisplayName = "JiraParser ExtractJiraTask returns only first matched project tasks in uppercase distinct form.")]
    [Trait("Category", "Unit")]
    public void ExtractJiraTaskReturnsOnlyFirstMatchedProjectTasksInUppercaseDistinctForm()
    {
        // Arrange
        var sut = new JiraParser();
        var commitInfo = new CommitInfo("fix xyz-2 and proj-1 then PROJ-1 and XYZ-3");
        IReadOnlyList<JiraProjectName> projectNames = [new JiraProjectName("PROJ"), new JiraProjectName("XYZ")];

        // Act
        var result = sut.ExtractJiraTask(commitInfo, projectNames);

        // Assert
        result.Value.Should().Be("XYZ-2, XYZ-3");
    }

    [Fact(DisplayName = "JiraParser SplitJiraTasks returns empty for not available value.")]
    [Trait("Category", "Unit")]
    public void SplitJiraTasksReturnsEmptyForNotAvailableValue()
    {
        // Arrange
        var sut = new JiraParser();

        // Act
        var result = sut.SplitJiraTasks(JiraTaskReference.NotAvailable);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "JiraParser SplitJiraTasks returns distinct trimmed task references.")]
    [Trait("Category", "Unit")]
    public void SplitJiraTasksReturnsDistinctTrimmedTaskReferences()
    {
        // Arrange
        var sut = new JiraParser();
        var jiraTask = new JiraTaskReference("PROJ-1, proj-1, PROJ-2");

        // Act
        var result = sut.SplitJiraTasks(jiraTask);

        // Assert
        result.Should().HaveCount(2);
        result[0].Value.Should().Be("PROJ-1");
        result[1].Value.Should().Be("PROJ-2");
    }

    [Fact(DisplayName = "JiraParser HasDependencyIssue throws when project names are null.")]
    [Trait("Category", "Unit")]
    public void HasDependencyIssueThrowsWhenProjectNamesAreNull()
    {
        // Arrange
        var sut = new JiraParser();
        var currentTask = new JiraTaskReference("PROJ-1");
        var alertDetails = new JiraAlertDetails("See PROJ-2", null);

        // Act
        Action action = () => _ = sut.HasDependencyIssue(currentTask, alertDetails, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("projectNames");
    }

    [Fact(DisplayName = "JiraParser HasDependencyIssue returns false when project list is empty.")]
    [Trait("Category", "Unit")]
    public void HasDependencyIssueReturnsFalseWhenProjectListIsEmpty()
    {
        // Arrange
        var sut = new JiraParser();
        var currentTask = new JiraTaskReference("PROJ-1");
        var alertDetails = new JiraAlertDetails("See PROJ-2", null);
        IReadOnlyList<JiraProjectName> projectNames = [];

        // Act
        var result = sut.HasDependencyIssue(currentTask, alertDetails, projectNames);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "JiraParser HasDependencyIssue returns false when details are empty.")]
    [Trait("Category", "Unit")]
    public void HasDependencyIssueReturnsFalseWhenDetailsAreEmpty()
    {
        // Arrange
        var sut = new JiraParser();
        var currentTask = new JiraTaskReference("PROJ-1");
        var alertDetails = new JiraAlertDetails(" ", null);
        IReadOnlyList<JiraProjectName> projectNames = [new JiraProjectName("PROJ")];

        // Act
        var result = sut.HasDependencyIssue(currentTask, alertDetails, projectNames);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "JiraParser HasDependencyIssue returns false when only current task is referenced.")]
    [Trait("Category", "Unit")]
    public void HasDependencyIssueReturnsFalseWhenOnlyCurrentTaskIsReferenced()
    {
        // Arrange
        var sut = new JiraParser();
        var currentTask = new JiraTaskReference("PROJ-1");
        var alertDetails = new JiraAlertDetails("depends on PROJ-1", "and PROJ-1");
        IReadOnlyList<JiraProjectName> projectNames = [new JiraProjectName("PROJ")];

        // Act
        var result = sut.HasDependencyIssue(currentTask, alertDetails, projectNames);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "JiraParser HasDependencyIssue returns true when another project task is referenced.")]
    [Trait("Category", "Unit")]
    public void HasDependencyIssueReturnsTrueWhenAnotherProjectTaskIsReferenced()
    {
        // Arrange
        var sut = new JiraParser();
        var currentTask = new JiraTaskReference("PROJ-1");
        var alertDetails = new JiraAlertDetails("depends on PROJ-2", "also check XYZ-8");
        IReadOnlyList<JiraProjectName> projectNames =
        [
            new JiraProjectName("PROJ"),
            new JiraProjectName("XYZ"),
        ];

        // Act
        var result = sut.HasDependencyIssue(currentTask, alertDetails, projectNames);

        // Assert
        result.Should().BeTrue();
    }
}
