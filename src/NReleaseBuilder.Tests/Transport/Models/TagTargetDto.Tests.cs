using FluentAssertions;

using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Tests.Transport.Models;

public class TagTargetDtoTests
{
    [Fact(DisplayName = "TagTargetDto hash can be assigned.")]
    [Trait("Category", "Unit")]
    public void HashCanBeAssigned()
    {
        // Arrange
        var dto = new TagTargetDto
        {
            Hash = "deadbeef",
        };

        // Act
        var result = dto.Hash;

        // Assert
        result.Should().Be("deadbeef");
    }

    [Fact(DisplayName = "TagTargetDto hash is null by default.")]
    [Trait("Category", "Unit")]
    public void HashIsNullByDefault()
    {
        // Arrange
        var dto = new TagTargetDto();

        // Act
        var result = dto.Hash;

        // Assert
        result.Should().BeNull();
    }
}
