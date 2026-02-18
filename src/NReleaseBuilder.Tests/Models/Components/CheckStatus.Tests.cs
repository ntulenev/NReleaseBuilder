using FluentAssertions;

using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Tests.Models.Components;

public class CheckStatusTests
{
    [Fact(DisplayName = "CheckStatus defines expected values.")]
    [Trait("Category", "Unit")]
    public void DefinesExpectedValues()
    {
        // Arrange
        // Act
        var values = Enum.GetValues<CheckStatus>();

        // Assert
        values.Should().ContainInOrder(
            CheckStatus.UpToDate,
            CheckStatus.Outdated,
            CheckStatus.RepositoryNotFound,
            CheckStatus.BitbucketError,
            CheckStatus.InvalidCurrentVersion);
    }
}
