using FluentAssertions;

using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Tests.Transport.Models;

public class JiraStatusDtoTests
{
    [Fact(DisplayName = "JiraStatusDto name can be assigned.")]
    [Trait("Category", "Unit")]
    public void NameCanBeAssigned()
    {
        // Arrange
        var dto = new JiraStatusDto
        {
            Name = "Done",
        };

        // Act
        var result = dto.Name;

        // Assert
        result.Should().Be("Done");
    }

    [Fact(DisplayName = "JiraStatusDto name is null by default.")]
    [Trait("Category", "Unit")]
    public void NameIsNullByDefault()
    {
        // Arrange
        var dto = new JiraStatusDto();

        // Act
        var result = dto.Name;

        // Assert
        result.Should().BeNull();
    }
}
