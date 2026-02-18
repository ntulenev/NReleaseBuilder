using FluentAssertions;

using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Jira;

public class JiraStatusNameTests
{
    [Fact(DisplayName = "JiraStatusName throws for invalid value.")]
    [Trait("Category", "Unit")]
    public void ThrowsForInvalidValue()
    {
        // Arrange
        // Act
        Action action = () => _ = new JiraStatusName(" ");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "JiraStatusName stores trimmed value and returns it from ToString.")]
    [Trait("Category", "Unit")]
    public void StoresTrimmedValueAndReturnsItFromToString()
    {
        // Arrange
        var status = new JiraStatusName("  Done  ");

        // Act
        var value = status.Value;
        var text = status.ToString();

        // Assert
        value.Should().Be("Done");
        text.Should().Be("Done");
    }

    [Fact(DisplayName = "JiraStatusName TryCreate returns false for invalid input.")]
    [Trait("Category", "Unit")]
    public void TryCreateReturnsFalseForInvalidInput()
    {
        // Arrange
        // Act
        var success = JiraStatusName.TryCreate(" ", out var status);

        // Assert
        success.Should().BeFalse();
        status.Should().Be(default(JiraStatusName));
    }

    [Fact(DisplayName = "JiraStatusName compares case-insensitively and supports operators.")]
    [Trait("Category", "Unit")]
    public void ComparesCaseInsensitivelyAndSupportsOperators()
    {
        // Arrange
        var left = new JiraStatusName("Done");
        var same = new JiraStatusName("done");
        var other = new JiraStatusName("In Progress");

        // Act
        var equals = left == same;
        var notEquals = left != other;
        var lessOrGreater = left.CompareTo(other);

        // Assert
        equals.Should().BeTrue();
        notEquals.Should().BeTrue();
        left.GetHashCode().Should().Be(same.GetHashCode());
        (lessOrGreater != 0).Should().BeTrue();
        (left <= same).Should().BeTrue();
        (left >= same).Should().BeTrue();
    }
}
