using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;

namespace NReleaseBuilder.Tests.Models.Bitbucket;

public class RepositoryNameTests
{
    [Fact(DisplayName = "RepositoryName throws for invalid value.")]
    [Trait("Category", "Unit")]
    public void ThrowsForInvalidValue()
    {
        // Arrange
        // Act
        Action action = () => _ = new RepositoryName(" ");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "RepositoryName stores trimmed value.")]
    [Trait("Category", "Unit")]
    public void StoresTrimmedValue()
    {
        // Arrange
        var value = new RepositoryName("  Repo.Service  ");

        // Act
        var raw = value.Value;
        var text = value.ToString();

        // Assert
        raw.Should().Be("Repo.Service");
        text.Should().Be("Repo.Service");
    }

    [Fact(DisplayName = "RepositoryName TryCreate returns false for invalid input.")]
    [Trait("Category", "Unit")]
    public void TryCreateReturnsFalseForInvalidInput()
    {
        // Arrange
        // Act
        var success = RepositoryName.TryCreate(" ", out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().Be(default(RepositoryName));
    }

    [Fact(DisplayName = "RepositoryName TryCreate returns true for valid input.")]
    [Trait("Category", "Unit")]
    public void TryCreateReturnsTrueForValidInput()
    {
        // Arrange
        // Act
        var success = RepositoryName.TryCreate("  repo-api  ", out var result);

        // Assert
        success.Should().BeTrue();
        result.Value.Should().Be("repo-api");
    }

    [Fact(DisplayName = "RepositoryName compares case-insensitively and supports ordering operators.")]
    [Trait("Category", "Unit")]
    public void ComparesCaseInsensitivelyAndSupportsOrderingOperators()
    {
        // Arrange
        var left = new RepositoryName("Repo-A");
        var right = new RepositoryName("repo-a");
        var greater = new RepositoryName("repo-b");

        // Act
        var equals = left == right;
        var notEquals = left != greater;
        var lessThan = left < greater;
        var greaterThan = greater > left;
        var lessOrEqual = left <= right;
        var greaterOrEqual = greater >= left;

        // Assert
        equals.Should().BeTrue();
        notEquals.Should().BeTrue();
        lessThan.Should().BeTrue();
        greaterThan.Should().BeTrue();
        lessOrEqual.Should().BeTrue();
        greaterOrEqual.Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }
}
