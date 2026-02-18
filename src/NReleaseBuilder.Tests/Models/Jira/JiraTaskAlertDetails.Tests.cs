using FluentAssertions;

using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Jira;

public class JiraTaskAlertDetailsTests
{
    [Fact(DisplayName = "JiraTaskAlertDetails normalizes optional values and sets flags.")]
    [Trait("Category", "Unit")]
    public void NormalizesOptionalValuesAndSetsFlags()
    {
        // Arrange
        var details = new JiraTaskAlertDetails(
            new JiraTaskReference("PROJ-1"),
            new JiraTitleReference("Issue"),
            new JiraStatusReference("Done"),
            "  Required  ",
            "  Breaking  ");

        // Act
        var required = details.RequiredActionsDetails;
        var breaking = details.BreakingChangesDetails;

        // Assert
        required.Should().Be("Required");
        breaking.Should().Be("Breaking");
        details.HasRequiredActions.Should().BeTrue();
        details.HasBreakingChanges.Should().BeTrue();
    }

    [Fact(DisplayName = "JiraTaskAlertDetails has false flags for empty optional values.")]
    [Trait("Category", "Unit")]
    public void HasFalseFlagsForEmptyOptionalValues()
    {
        // Arrange
        var details = new JiraTaskAlertDetails(
            new JiraTaskReference("PROJ-1"),
            new JiraTitleReference("Issue"),
            new JiraStatusReference("Done"),
            " ",
            null);

        // Act
        var required = details.HasRequiredActions;
        var breaking = details.HasBreakingChanges;

        // Assert
        details.RequiredActionsDetails.Should().BeNull();
        details.BreakingChangesDetails.Should().BeNull();
        required.Should().BeFalse();
        breaking.Should().BeFalse();
    }
}
