using FluentAssertions;

using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Jira;

public class JiraAlertDetailsTests
{
    [Fact(DisplayName = "JiraAlertDetails normalizes empty values to null.")]
    [Trait("Category", "Unit")]
    public void NormalizesEmptyValuesToNull()
    {
        // Arrange
        var details = new JiraAlertDetails(" ", null);

        // Act
        var requiredActions = details.RequiredActionsDetails;
        var breakingChanges = details.BreakingChangesDetails;

        // Assert
        requiredActions.Should().BeNull();
        breakingChanges.Should().BeNull();
    }

    [Fact(DisplayName = "JiraAlertDetails trims values.")]
    [Trait("Category", "Unit")]
    public void TrimsValues()
    {
        // Arrange
        var details = new JiraAlertDetails("  do this  ", "  breaking  ");

        // Act
        var requiredActions = details.RequiredActionsDetails;
        var breakingChanges = details.BreakingChangesDetails;

        // Assert
        requiredActions.Should().Be("do this");
        breakingChanges.Should().Be("breaking");
    }
}
