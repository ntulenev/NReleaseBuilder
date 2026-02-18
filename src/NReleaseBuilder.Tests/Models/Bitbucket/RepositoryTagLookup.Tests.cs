using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Models.Bitbucket;

public class RepositoryTagLookupTests
{
    [Fact(DisplayName = "RepositoryTagLookup throws when tags are null.")]
    [Trait("Category", "Unit")]
    public void ThrowsWhenTagsAreNull()
    {
        // Arrange
        var repository = new RepositoryName("repo");

        // Act
        Action action = () => _ = new RepositoryTagLookup(repository, false, null, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("tags");
    }

    [Fact(DisplayName = "RepositoryTagLookup throws when resolved repository is empty.")]
    [Trait("Category", "Unit")]
    public void ThrowsWhenResolvedRepositoryIsEmpty()
    {
        // Arrange
        var emptyRepository = default(RepositoryName);

        // Act
        Action action = () => _ = new RepositoryTagLookup(emptyRepository, false, null, []);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("resolvedRepository");
    }

    [Fact(DisplayName = "RepositoryTagLookup throws when repository missing and error is provided.")]
    [Trait("Category", "Unit")]
    public void ThrowsWhenRepositoryMissingAndErrorIsProvided()
    {
        // Arrange
        var repository = new RepositoryName("repo");

        // Act
        Action action = () => _ = new RepositoryTagLookup(repository, true, "error", []);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("error");
    }

    [Fact(DisplayName = "RepositoryTagLookup normalizes error and stores values.")]
    [Trait("Category", "Unit")]
    public void NormalizesErrorAndStoresValues()
    {
        // Arrange
        var repository = new RepositoryName("repo");
        IReadOnlyList<RepositoryTagInfo> tags =
        [
            new RepositoryTagInfo(
                new VersionLabel("1.0.0"),
                new JiraTaskReference("PROJ-1"),
                new JiraTitleReference("Issue"),
                new JiraStatusReference("Done"),
                [],
                false,
                false,
                false),
        ];

        // Act
        var lookup = new RepositoryTagLookup(repository, false, "  failed  ", tags);

        // Assert
        lookup.ResolvedRepository.Should().Be(repository);
        lookup.IsRepositoryMissing.Should().BeFalse();
        lookup.Error.Should().Be("failed");
        lookup.Tags.Should().BeSameAs(tags);
    }

    [Fact(DisplayName = "RepositoryTagLookup static factories create expected values.")]
    [Trait("Category", "Unit")]
    public void StaticFactoriesCreateExpectedValues()
    {
        // Arrange
        var repository = new RepositoryName("repo");
        IReadOnlyList<RepositoryTagInfo> tags = [];

        // Act
        var missing = RepositoryTagLookup.RepoNotFound(repository);
        var error = RepositoryTagLookup.ApiError(repository, "api error");
        var success = RepositoryTagLookup.Success(repository, tags);

        // Assert
        missing.IsRepositoryMissing.Should().BeTrue();
        missing.Error.Should().BeNull();
        missing.Tags.Should().BeEmpty();

        error.IsRepositoryMissing.Should().BeFalse();
        error.Error.Should().Be("api error");
        error.Tags.Should().BeEmpty();

        success.IsRepositoryMissing.Should().BeFalse();
        success.Error.Should().BeNull();
        success.Tags.Should().BeSameAs(tags);
    }
}
