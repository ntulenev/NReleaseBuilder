using FluentAssertions;

using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Jira;

public class JiraTaskResolutionTests
{
    [Fact(DisplayName = "JiraTaskResolution throws when task alert details are null in direct constructor.")]
    [Trait("Category", "Unit")]
    public void ThrowsWhenTaskAlertDetailsAreNullInDirectConstructor()
    {
        // Arrange
        // Act
        Action action = () => _ = new JiraTaskResolution(
            new JiraStatusReference("Done"),
            new JiraTaskReference("PROJ-1"),
            new JiraTitleReference("Issue"),
            null!,
            hasRequiredActions: false,
            hasBreakingChanges: false,
            hasDependencyIssues: false);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("taskAlertDetails");
    }

    [Fact(DisplayName = "JiraTaskResolution list constructor combines values.")]
    [Trait("Category", "Unit")]
    public void ListConstructorCombinesValues()
    {
        // Arrange
        IReadOnlyList<JiraStatusReference> statuses = [new("Done"), new("In Progress")];
        IReadOnlyList<JiraTaskReference> tasks = [new("PROJ-1"), new("PROJ-2")];
        IReadOnlyList<JiraTitleReference> titles = [new("Issue A"), new("Issue B")];
        IReadOnlyList<JiraTaskAlertDetails> details = [];

        // Act
        var result = new JiraTaskResolution(
            statuses,
            tasks,
            titles,
            details,
            hasRequiredActions: true,
            hasBreakingChanges: false,
            hasDependencyIssues: true);

        // Assert
        result.Statuses.Value.Should().Be("Done, In Progress");
        result.Tasks.Value.Should().Be("PROJ-1, PROJ-2");
        result.Titles.Value.Should().Be("Issue A, Issue B");
        result.TaskAlertDetails.Should().BeSameAs(details);
        result.HasRequiredActions.Should().BeTrue();
        result.HasBreakingChanges.Should().BeFalse();
        result.HasDependencyIssues.Should().BeTrue();
    }

    [Fact(DisplayName = "JiraTaskResolution NotAvailable returns fallback values.")]
    [Trait("Category", "Unit")]
    public void NotAvailableReturnsFallbackValues()
    {
        // Arrange
        var task = new JiraTaskReference("PROJ-1");

        // Act
        var result = JiraTaskResolution.NotAvailable(task);

        // Assert
        result.Statuses.Value.Should().Be("N/A");
        result.Tasks.Should().Be(task);
        result.Titles.Value.Should().Be("N/A");
        result.TaskAlertDetails.Should().BeEmpty();
        result.HasRequiredActions.Should().BeFalse();
        result.HasBreakingChanges.Should().BeFalse();
        result.HasDependencyIssues.Should().BeFalse();
    }
}
