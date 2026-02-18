using FluentAssertions;

using NReleaseBuilder.Models;

namespace NReleaseBuilder.Tests.Models;

public class ErrorMessageTests
{
    [Fact(DisplayName = "ErrorMessage throws for invalid value.")]
    [Trait("Category", "Unit")]
    public void ThrowsForInvalidValue()
    {
        // Arrange
        // Act
        Action action = () => _ = new ErrorMessage(" ");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "ErrorMessage stores trimmed value and returns it from ToString.")]
    [Trait("Category", "Unit")]
    public void StoresTrimmedValueAndReturnsItFromToString()
    {
        // Arrange
        var error = new ErrorMessage("  failed request  ");

        // Act
        var value = error.Value;
        var text = error.ToString();

        // Assert
        value.Should().Be("failed request");
        text.Should().Be("failed request");
    }
}
