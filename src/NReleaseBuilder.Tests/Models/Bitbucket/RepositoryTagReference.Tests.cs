using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;

namespace NReleaseBuilder.Tests.Models.Bitbucket;

public class RepositoryTagReferenceTests
{
    [Fact(DisplayName = "RepositoryTagReference stores values with commit hash.")]
    [Trait("Category", "Unit")]
    public void StoresValuesWithCommitHash()
    {
        // Arrange
        var reference = new RepositoryTagReference(
            new VersionLabel("1.2.3"),
            new CommitHash("abc123"));

        // Act
        var name = reference.Name.Value;
        var commitHash = reference.CommitHash;

        // Assert
        name.Should().Be("1.2.3");
        commitHash.Should().NotBeNull();
        commitHash!.Value.Value.Should().Be("abc123");
    }

    [Fact(DisplayName = "RepositoryTagReference stores null commit hash.")]
    [Trait("Category", "Unit")]
    public void StoresNullCommitHash()
    {
        // Arrange
        var reference = new RepositoryTagReference(
            new VersionLabel("1.2.3"),
            null);

        // Act
        var commitHash = reference.CommitHash;

        // Assert
        commitHash.Should().BeNull();
    }
}
