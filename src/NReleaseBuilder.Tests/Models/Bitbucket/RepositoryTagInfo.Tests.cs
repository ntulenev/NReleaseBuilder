using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Bitbucket;

public class RepositoryTagInfoTests
{
    [Fact(DisplayName = "RepositoryTagInfo throws when task alert details are null.")]
    [Trait("Category", "Unit")]
    public void ThrowsWhenTaskAlertDetailsAreNull()
    {
        // Arrange
        // Act
        Action action = () => _ = new RepositoryTagInfo(
            new VersionLabel("1.0.0"),
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

    [Fact(DisplayName = "RepositoryTagInfo stores values.")]
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
                "Required",
                "Breaking"),
        ];
        var pullRequestUrl = new Uri("https://bitbucket.example.test/workspace/repo/pull-requests/10");

        var info = new RepositoryTagInfo(
            new VersionLabel("1.0.0"),
            new JiraTaskReference("PROJ-1"),
            new JiraTitleReference("Issue"),
            new JiraStatusReference("Done"),
            details,
            hasRequiredActions: true,
            hasBreakingChanges: true,
            hasDependencyIssues: true,
            pullRequestUrl);

        // Act
        var version = info.Name.Value;
        var jiraTask = info.JiraTask.Value;
        var jiraTitle = info.JiraTitle.Value;
        var jiraStatus = info.JiraStatus.Value;
        var required = info.HasRequiredActions;
        var breaking = info.HasBreakingChanges;
        var dependency = info.HasDependencyIssues;

        // Assert
        version.Should().Be("1.0.0");
        jiraTask.Should().Be("PROJ-1");
        jiraTitle.Should().Be("Issue");
        jiraStatus.Should().Be("Done");
        info.TaskAlertDetails.Should().BeSameAs(details);
        required.Should().BeTrue();
        breaking.Should().BeTrue();
        dependency.Should().BeTrue();
        info.PullRequestUrl.Should().Be(pullRequestUrl);
    }
}
