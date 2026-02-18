using FluentAssertions;

using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Jira;

public class JiraTaskReferenceTests
{
    [Fact(DisplayName = "JiraTaskReference throws for invalid value.")]
    [Trait("Category", "Unit")]
    public void ThrowsForInvalidValue()
    {
        // Arrange
        // Act
        Action action = () => _ = new JiraTaskReference(" ");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "JiraTaskReference stores trimmed value and returns it from ToString.")]
    [Trait("Category", "Unit")]
    public void StoresTrimmedValueAndReturnsItFromToString()
    {
        // Arrange
        var reference = new JiraTaskReference("  PROJ-1  ");

        // Act
        var value = reference.Value;
        var text = reference.ToString();

        // Assert
        value.Should().Be("PROJ-1");
        text.Should().Be("PROJ-1");
    }

    [Fact(DisplayName = "JiraTaskReference exposes not available value.")]
    [Trait("Category", "Unit")]
    public void ExposesNotAvailableValue()
    {
        // Arrange
        // Act
        var notAvailable = JiraTaskReference.NotAvailable;

        // Assert
        notAvailable.Value.Should().Be("N/A");
    }
}
