using FluentAssertions;

using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Tests.Models.Components;

public class RowDetailsTests
{
    [Fact(DisplayName = "RowDetails throws for invalid value.")]
    [Trait("Category", "Unit")]
    public void ThrowsForInvalidValue()
    {
        // Arrange
        // Act
        Action action = () => _ = new RowDetails(" ");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "RowDetails stores trimmed value and returns it from ToString.")]
    [Trait("Category", "Unit")]
    public void StoresTrimmedValueAndReturnsItFromToString()
    {
        // Arrange
        var details = new RowDetails("  up to date  ");

        // Act
        var value = details.Value;
        var text = details.ToString();

        // Assert
        value.Should().Be("up to date");
        text.Should().Be("up to date");
    }
}
