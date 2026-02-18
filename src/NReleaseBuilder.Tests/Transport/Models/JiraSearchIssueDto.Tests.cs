using FluentAssertions;

using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Tests.Transport.Models;

public class JiraSearchIssueDtoTests
{
    [Fact(DisplayName = "JiraSearchIssueDto fields can be assigned.")]
    [Trait("Category", "Unit")]
    public void FieldsCanBeAssigned()
    {
        // Arrange
        var fields = new JiraIssueFieldsDto
        {
            Status = new JiraStatusDto { Name = "To Do" },
            Summary = "Document release process",
        };

        var dto = new JiraSearchIssueDto
        {
            Fields = fields,
        };

        // Act
        var result = dto.Fields;

        // Assert
        result.Should().BeSameAs(fields);
    }

    [Fact(DisplayName = "JiraSearchIssueDto fields are null by default.")]
    [Trait("Category", "Unit")]
    public void FieldsAreNullByDefault()
    {
        // Arrange
        var dto = new JiraSearchIssueDto();

        // Act
        var result = dto.Fields;

        // Assert
        result.Should().BeNull();
    }
}
