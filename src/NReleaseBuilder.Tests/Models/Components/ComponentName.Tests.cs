using FluentAssertions;

using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Tests.Models.Components;

public class ComponentNameTests
{
    [Fact(DisplayName = "ComponentName throws for invalid value.")]
    [Trait("Category", "Unit")]
    public void ThrowsForInvalidValue()
    {
        // Arrange
        // Act
        Action action = () => _ = new ComponentName(" ");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "ComponentName stores trimmed value and returns it from ToString.")]
    [Trait("Category", "Unit")]
    public void StoresTrimmedValueAndReturnsItFromToString()
    {
        // Arrange
        var value = new ComponentName("  api  ");

        // Act
        var raw = value.Value;
        var text = value.ToString();

        // Assert
        raw.Should().Be("api");
        text.Should().Be("api");
    }
}
