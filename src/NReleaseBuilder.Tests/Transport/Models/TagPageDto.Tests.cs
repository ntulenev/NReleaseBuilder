using FluentAssertions;

using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Tests.Transport.Models;

public class TagPageDtoTests
{
    [Fact(DisplayName = "TagPageDto properties can be assigned.")]
    [Trait("Category", "Unit")]
    public void PropertiesCanBeAssigned()
    {
        // Arrange
        IReadOnlyList<TagDto> values =
        [
            new TagDto
            {
                Name = "v1.0.0",
                Target = new TagTargetDto { Hash = "abc123" },
            },
        ];

        var dto = new TagPageDto
        {
            Values = values,
            Next = "https://example.test/next",
        };

        // Act
        var resultValues = dto.Values;
        var resultNext = dto.Next;

        // Assert
        resultValues.Should().BeSameAs(values);
        resultNext.Should().Be("https://example.test/next");
    }

    [Fact(DisplayName = "TagPageDto properties are null by default.")]
    [Trait("Category", "Unit")]
    public void PropertiesAreNullByDefault()
    {
        // Arrange
        var dto = new TagPageDto();

        // Act
        var resultValues = dto.Values;
        var resultNext = dto.Next;

        // Assert
        resultValues.Should().BeNull();
        resultNext.Should().BeNull();
    }
}
