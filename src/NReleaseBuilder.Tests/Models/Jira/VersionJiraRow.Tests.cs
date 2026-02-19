using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Jira;

public class VersionJiraRowTests
{
    [Fact(DisplayName = "VersionJiraRow throws when task alert details are null.")]
    [Trait("Category", "Unit")]
    public void ThrowsWhenTaskAlertDetailsAreNull()
    {
        // Arrange
        // Act
        Action action = () => _ = new VersionJiraRow(
            new VersionLabel("1.1.0"),
            new JiraTaskReference("PROJ-1"),
            new JiraTitleReference("Issue"),
            new JiraStatusReference("Done"),
            null!,
            hasRequiredActions: false,
            hasBreakingChanges: false,
            hasDependencyIssues: false);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("taskAlertDetails");
    }

    [Fact(DisplayName = "VersionJiraRow stores values.")]
    [Trait("Category", "Unit")]
    public void StoresValues()
    {
        // Arrange
        IReadOnlyList<JiraTaskAlertDetails> details =
        [
            new JiraTaskAlertDetails(
                new JiraTaskReference("PROJ-1"),
                new JiraTitleReference("Issue"),
                new JiraStatusReference("Done"),
                null,
                null),
        ];
        var pullRequestUrl = new Uri("https://bitbucket.example.test/workspace/repo/pull-requests/11");

        var row = new VersionJiraRow(
            new VersionLabel("1.1.0"),
            new JiraTaskReference("PROJ-1"),
            new JiraTitleReference("Issue"),
            new JiraStatusReference("Done"),
            details,
            hasRequiredActions: true,
            hasBreakingChanges: false,
            hasDependencyIssues: true,
            pullRequestUrl);

        // Act
        var version = row.Version.Value;
        var task = row.JiraTask.Value;
        var title = row.JiraTitle.Value;
        var status = row.JiraStatus.Value;

        // Assert
        version.Should().Be("1.1.0");
        task.Should().Be("PROJ-1");
        title.Should().Be("Issue");
        status.Should().Be("Done");
        row.TaskAlertDetails.Should().BeSameAs(details);
        row.HasRequiredActions.Should().BeTrue();
        row.HasBreakingChanges.Should().BeFalse();
        row.HasDependencyIssues.Should().BeTrue();
        row.PullRequestUrl.Should().Be(pullRequestUrl);
    }
}
