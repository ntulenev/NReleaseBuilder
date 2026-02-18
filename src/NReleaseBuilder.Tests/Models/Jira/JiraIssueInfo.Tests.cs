using FluentAssertions;

using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Jira;

public class JiraIssueInfoTests
{
    [Fact(DisplayName = "JiraIssueInfo throws for invalid title.")]
    [Trait("Category", "Unit")]
    public void ThrowsForInvalidTitle()
    {
        // Arrange
        // Act
        Action action = () => _ = new JiraIssueInfo(
            new JiraStatusName("Done"),
            " ",
            null,
            null);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "JiraIssueInfo stores normalized values and flags.")]
    [Trait("Category", "Unit")]
    public void StoresNormalizedValuesAndFlags()
    {
        // Arrange
        var issue = new JiraIssueInfo(
            new JiraStatusName("Done"),
            "  Task title  ",
            "  Required actions  ",
            "  Breaking changes  ");

        // Act
        var status = issue.StatusName;
        var title = issue.Title;
        var required = issue.RequiredActionsDetails;
        var breaking = issue.BreakingChangesDetails;

        // Assert
        status.Should().NotBeNull();
        status!.Value.Value.Should().Be("Done");
        title.Should().Be("Task title");
        required.Should().Be("Required actions");
        breaking.Should().Be("Breaking changes");
        issue.HasRequiredActions.Should().BeTrue();
        issue.HasBreakingChanges.Should().BeTrue();
    }

    [Fact(DisplayName = "JiraIssueInfo has false flags for empty optional values.")]
    [Trait("Category", "Unit")]
    public void HasFalseFlagsForEmptyOptionalValues()
    {
        // Arrange
        var issue = new JiraIssueInfo(
            statusName: null,
            title: "Task title",
            requiredActionsDetails: " ",
            breakingChangesDetails: null);

        // Act
        var required = issue.HasRequiredActions;
        var breaking = issue.HasBreakingChanges;

        // Assert
        required.Should().BeFalse();
        breaking.Should().BeFalse();
    }
}
