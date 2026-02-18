using FluentAssertions;

using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Jira;

public class JiraProjectNameTests
{
    [Fact(DisplayName = "JiraProjectName throws for invalid value.")]
    [Trait("Category", "Unit")]
    public void ThrowsForInvalidValue()
    {
        // Arrange
        // Act
        Action action = () => _ = new JiraProjectName(" ");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "JiraProjectName stores trimmed value and returns it from ToString.")]
    [Trait("Category", "Unit")]
    public void StoresTrimmedValueAndReturnsItFromToString()
    {
        // Arrange
        var project = new JiraProjectName("  PROJ  ");

        // Act
        var value = project.Value;
        var text = project.ToString();

        // Assert
        value.Should().Be("PROJ");
        text.Should().Be("PROJ");
    }
}
