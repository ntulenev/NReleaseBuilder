using FluentAssertions;

using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Jira;

public class JiraTitleReferenceTests
{
    [Fact(DisplayName = "JiraTitleReference throws for invalid value.")]
    [Trait("Category", "Unit")]
    public void ThrowsForInvalidValue()
    {
        // Arrange
        // Act
        Action action = () => _ = new JiraTitleReference(" ");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "JiraTitleReference stores trimmed value and returns it from ToString.")]
    [Trait("Category", "Unit")]
    public void StoresTrimmedValueAndReturnsItFromToString()
    {
        // Arrange
        var reference = new JiraTitleReference("  Issue title  ");

        // Act
        var value = reference.Value;
        var text = reference.ToString();

        // Assert
        value.Should().Be("Issue title");
        text.Should().Be("Issue title");
    }
}
