using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;

namespace NReleaseBuilder.Tests.Models.Bitbucket;

public class CommitInfoTests
{
    [Fact(DisplayName = "CommitInfo trims message.")]
    [Trait("Category", "Unit")]
    public void TrimsMessage()
    {
        // Arrange
        var info = new CommitInfo("  fix bug  ");

        // Act
        var message = info.Message;

        // Assert
        message.Should().Be("fix bug");
    }

    [Fact(DisplayName = "CommitInfo returns null message for empty input.")]
    [Trait("Category", "Unit")]
    public void ReturnsNullMessageForEmptyInput()
    {
        // Arrange
        var info = new CommitInfo("  ");

        // Act
        var message = info.Message;

        // Assert
        message.Should().BeNull();
    }
}
