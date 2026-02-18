using FluentAssertions;

using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Tests.Transport.Models;

public class CommitDtoTests
{
    [Fact(DisplayName = "CommitDto message can be assigned.")]
    [Trait("Category", "Unit")]
    public void MessageCanBeAssigned()
    {
        // Arrange
        var dto = new CommitDto
        {
            Message = "feat: add rollback command",
        };

        // Act
        var result = dto.Message;

        // Assert
        result.Should().Be("feat: add rollback command");
    }

    [Fact(DisplayName = "CommitDto message is null by default.")]
    [Trait("Category", "Unit")]
    public void MessageIsNullByDefault()
    {
        // Arrange
        var dto = new CommitDto();

        // Act
        var result = dto.Message;

        // Assert
        result.Should().BeNull();
    }
}
