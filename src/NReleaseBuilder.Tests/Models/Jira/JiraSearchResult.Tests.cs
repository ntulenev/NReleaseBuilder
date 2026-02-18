using FluentAssertions;

using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Jira;

public class JiraSearchResultTests
{
    [Fact(DisplayName = "JiraSearchResult throws when issues are null.")]
    [Trait("Category", "Unit")]
    public void ThrowsWhenIssuesAreNull()
    {
        // Arrange
        // Act
        Action action = () => _ = new JiraSearchResult(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("issues");
    }

    [Fact(DisplayName = "JiraSearchResult throws when issues contain null item.")]
    [Trait("Category", "Unit")]
    public void ThrowsWhenIssuesContainNullItem()
    {
        // Arrange
        IReadOnlyList<JiraIssueInfo> issues = [null!];

        // Act
        Action action = () => _ = new JiraSearchResult(issues);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("issues");
    }

    [Fact(DisplayName = "JiraSearchResult stores issues.")]
    [Trait("Category", "Unit")]
    public void StoresIssues()
    {
        // Arrange
        IReadOnlyList<JiraIssueInfo> issues =
        [
            new JiraIssueInfo(new JiraStatusName("Done"), "Issue", null, null),
        ];

        // Act
        var result = new JiraSearchResult(issues);

        // Assert
        result.Issues.Should().BeSameAs(issues);
    }
}
