using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;

namespace NReleaseBuilder.Tests.Models.Bitbucket;

public class VersionLabelTests
{
    [Fact(DisplayName = "VersionLabel throws for invalid value.")]
    [Trait("Category", "Unit")]
    public void ThrowsForInvalidValue()
    {
        // Arrange
        // Act
        Action action = () => _ = new VersionLabel(" ");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "VersionLabel stores trimmed value and returns it from ToString.")]
    [Trait("Category", "Unit")]
    public void StoresTrimmedValueAndReturnsItFromToString()
    {
        // Arrange
        var label = new VersionLabel("  v1.2.3  ");

        // Act
        var value = label.Value;
        var text = label.ToString();

        // Assert
        value.Should().Be("v1.2.3");
        text.Should().Be("v1.2.3");
    }
}
