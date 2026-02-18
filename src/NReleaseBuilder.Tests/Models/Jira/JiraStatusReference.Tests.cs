using FluentAssertions;

using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Jira;

public class JiraStatusReferenceTests
{
    [Fact(DisplayName = "JiraStatusReference throws for invalid value.")]
    [Trait("Category", "Unit")]
    public void ThrowsForInvalidValue()
    {
        // Arrange
        // Act
        Action action = () => _ = new JiraStatusReference(" ");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "JiraStatusReference stores trimmed value and returns it from ToString.")]
    [Trait("Category", "Unit")]
    public void StoresTrimmedValueAndReturnsItFromToString()
    {
        // Arrange
        var reference = new JiraStatusReference("  Done, In Progress  ");

        // Act
        var value = reference.Value;
        var text = reference.ToString();

        // Assert
        value.Should().Be("Done, In Progress");
        text.Should().Be("Done, In Progress");
    }

    [Fact(DisplayName = "JiraStatusReference SplitStatuses returns distinct statuses.")]
    [Trait("Category", "Unit")]
    public void SplitStatusesReturnsDistinctStatuses()
    {
        // Arrange
        var reference = new JiraStatusReference("Done, done, In Progress");

        // Act
        var statuses = reference.SplitStatuses();

        // Assert
        statuses.Should().HaveCount(2);
        statuses.Should().Contain(new JiraStatusName("Done"));
        statuses.Should().Contain(new JiraStatusName("In Progress"));
    }
}
