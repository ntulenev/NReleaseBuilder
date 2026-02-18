using FluentAssertions;

using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Tests.Transport.Models;

public class JiraSearchResponseDtoTests
{
    [Fact(DisplayName = "JiraSearchResponseDto issues are empty by default.")]
    [Trait("Category", "Unit")]
    public void IssuesAreEmptyByDefault()
    {
        // Arrange
        var dto = new JiraSearchResponseDto();

        // Act
        var issues = dto.Issues;

        // Assert
        issues.Should().NotBeNull();
        issues.Should().BeEmpty();
    }

    [Fact(DisplayName = "JiraSearchResponseDto issues can be assigned.")]
    [Trait("Category", "Unit")]
    public void IssuesCanBeAssigned()
    {
        // Arrange
        IReadOnlyList<JiraSearchIssueDto> issues =
        [
            new JiraSearchIssueDto
            {
                Fields = new JiraIssueFieldsDto
                {
                    Summary = "Review release notes",
                },
            },
        ];

        var dto = new JiraSearchResponseDto
        {
            Issues = issues,
        };

        // Act
        var result = dto.Issues;

        // Assert
        result.Should().BeSameAs(issues);
    }
}
