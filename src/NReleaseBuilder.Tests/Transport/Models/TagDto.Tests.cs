using FluentAssertions;

using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Tests.Transport.Models;

public class TagDtoTests
{
    [Fact(DisplayName = "TagDto properties can be assigned.")]
    [Trait("Category", "Unit")]
    public void PropertiesCanBeAssigned()
    {
        // Arrange
        var target = new TagTargetDto
        {
            Hash = "sha256",
        };

        var dto = new TagDto
        {
            Name = "v2.3.4",
            Target = target,
        };

        // Act
        var resultName = dto.Name;
        var resultTarget = dto.Target;

        // Assert
        resultName.Should().Be("v2.3.4");
        resultTarget.Should().BeSameAs(target);
    }

    [Fact(DisplayName = "TagDto properties are null by default.")]
    [Trait("Category", "Unit")]
    public void PropertiesAreNullByDefault()
    {
        // Arrange
        var dto = new TagDto();

        // Act
        var resultName = dto.Name;
        var resultTarget = dto.Target;

        // Assert
        resultName.Should().BeNull();
        resultTarget.Should().BeNull();
    }
}
