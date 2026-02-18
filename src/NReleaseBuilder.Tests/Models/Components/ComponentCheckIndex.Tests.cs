using FluentAssertions;

using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Tests.Models.Components;

public class ComponentCheckIndexTests
{
    [Fact(DisplayName = "ComponentCheckIndex throws when value is less than one.")]
    [Trait("Category", "Unit")]
    public void ThrowsWhenValueIsLessThanOne()
    {
        // Arrange
        // Act
        Action action = () => _ = new ComponentCheckIndex(0);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "ComponentCheckIndex stores value and converts to string.")]
    [Trait("Category", "Unit")]
    public void StoresValueAndConvertsToString()
    {
        // Arrange
        var index = new ComponentCheckIndex(3);

        // Act
        var value = index.Value;
        var text = index.ToString();

        // Assert
        value.Should().Be(3);
        text.Should().Be("3");
    }
}
