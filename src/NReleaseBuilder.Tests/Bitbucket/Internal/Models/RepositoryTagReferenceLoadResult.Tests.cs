using FluentAssertions;

using NReleaseBuilder.Bitbucket.Internal.Models;
using NReleaseBuilder.Models.Bitbucket;

namespace NReleaseBuilder.Tests.Bitbucket.Internal.Models;

public class RepositoryTagReferenceLoadResultTests
{
    [Fact(DisplayName = "RepositoryTagReferenceLoadResult RepoNotFound creates missing result.")]
    [Trait("Category", "Unit")]
    public void RepoNotFoundCreatesMissingResult()
    {
        // Arrange
        // Act
        var result = RepositoryTagReferenceLoadResult.RepoNotFound();

        // Assert
        result.IsRepositoryMissing.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Tags.Should().BeEmpty();
    }

    [Fact(DisplayName = "RepositoryTagReferenceLoadResult ApiError throws for empty message.")]
    [Trait("Category", "Unit")]
    public void ApiErrorThrowsForEmptyMessage()
    {
        // Arrange
        // Act
        Action action = () => _ = RepositoryTagReferenceLoadResult.ApiError(" ");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "RepositoryTagReferenceLoadResult ApiError creates error result.")]
    [Trait("Category", "Unit")]
    public void ApiErrorCreatesErrorResult()
    {
        // Arrange
        // Act
        var result = RepositoryTagReferenceLoadResult.ApiError("bitbucket failure");

        // Assert
        result.IsRepositoryMissing.Should().BeFalse();
        result.Error.Should().Be("bitbucket failure");
        result.Tags.Should().BeEmpty();
    }

    [Fact(DisplayName = "RepositoryTagReferenceLoadResult Success throws when tags are null.")]
    [Trait("Category", "Unit")]
    public void SuccessThrowsWhenTagsAreNull()
    {
        // Arrange
        IReadOnlyList<RepositoryTagReference> tags = null!;

        // Act
        Action action = () => _ = RepositoryTagReferenceLoadResult.Success(tags);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("tags");
    }

    [Fact(DisplayName = "RepositoryTagReferenceLoadResult Success creates success result.")]
    [Trait("Category", "Unit")]
    public void SuccessCreatesSuccessResult()
    {
        // Arrange
        IReadOnlyList<RepositoryTagReference> tags =
        [
            new RepositoryTagReference(new VersionLabel("1.0.0"), new CommitHash("abc123")),
        ];

        // Act
        var result = RepositoryTagReferenceLoadResult.Success(tags);

        // Assert
        result.IsRepositoryMissing.Should().BeFalse();
        result.Error.Should().BeNull();
        result.Tags.Should().BeSameAs(tags);
    }
}
