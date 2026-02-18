using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;

namespace NReleaseBuilder.Tests.Models.Bitbucket;

public class CommitHashTests
{
    [Fact(DisplayName = "CommitHash throws for invalid value.")]
    [Trait("Category", "Unit")]
    public void ThrowsForInvalidValue()
    {
        // Arrange
        // Act
        Action action = () => _ = new CommitHash(" ");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "CommitHash stores trimmed value.")]
    [Trait("Category", "Unit")]
    public void StoresTrimmedValue()
    {
        // Arrange
        var hash = new CommitHash("  abc123  ");

        // Act
        var value = hash.Value;
        var text = hash.ToString();

        // Assert
        value.Should().Be("abc123");
        text.Should().Be("abc123");
    }

    [Fact(DisplayName = "CommitHash FromOptional returns null for empty input.")]
    [Trait("Category", "Unit")]
    public void FromOptionalReturnsNullForEmptyInput()
    {
        // Arrange
        // Act
        var result = CommitHash.FromOptional(" ");

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "CommitHash FromOptional returns trimmed hash for valid input.")]
    [Trait("Category", "Unit")]
    public void FromOptionalReturnsTrimmedHashForValidInput()
    {
        // Arrange
        // Act
        var result = CommitHash.FromOptional("  deadbeef  ");

        // Assert
        result.Should().NotBeNull();
        result!.Value.Value.Should().Be("deadbeef");
    }
}
