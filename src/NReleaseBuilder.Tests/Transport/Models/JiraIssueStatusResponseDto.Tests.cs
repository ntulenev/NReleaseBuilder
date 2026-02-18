using FluentAssertions;

using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Tests.Transport.Models;

public class JiraIssueStatusResponseDtoTests
{
    [Fact(DisplayName = "JiraIssueStatusResponseDto properties can be assigned.")]
    [Trait("Category", "Unit")]
    public void PropertiesCanBeAssigned()
    {
        // Arrange
        var fields = new JiraIssueFieldsDto
        {
            Status = new JiraStatusDto { Name = "Done" },
            Summary = "Release component",
        };
        IReadOnlyDictionary<string, string?> names = new Dictionary<string, string?>
        {
            ["customfield_10010"] = "Required Actions",
        };

        var dto = new JiraIssueStatusResponseDto
        {
            Fields = fields,
            Names = names,
        };

        // Act
        var resultFields = dto.Fields;
        var resultNames = dto.Names;

        // Assert
        resultFields.Should().BeSameAs(fields);
        resultNames.Should().BeSameAs(names);
    }

    [Fact(DisplayName = "JiraIssueStatusResponseDto properties are null by default.")]
    [Trait("Category", "Unit")]
    public void PropertiesAreNullByDefault()
    {
        // Arrange
        var dto = new JiraIssueStatusResponseDto();

        // Act
        var fields = dto.Fields;
        var names = dto.Names;

        // Assert
        fields.Should().BeNull();
        names.Should().BeNull();
    }
}
