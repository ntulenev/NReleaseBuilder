using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;

namespace NReleaseBuilder.Tests.Models.Bitbucket;

public class RepositoryTagPageTests
{
    [Fact(DisplayName = "RepositoryTagPage throws when values are null.")]
    [Trait("Category", "Unit")]
    public void ThrowsWhenValuesAreNull()
    {
        // Arrange
        // Act
        Action action = () => _ = new RepositoryTagPage(null!, null);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("values");
    }

    [Fact(DisplayName = "RepositoryTagPage stores values and next uri.")]
    [Trait("Category", "Unit")]
    public void StoresValuesAndNextUri()
    {
        // Arrange
        IReadOnlyList<RepositoryTagReference> values =
        [
            new RepositoryTagReference(new VersionLabel("1.0.0"), new CommitHash("abc")),
        ];
        var next = new Uri("https://example.test/next");
        var page = new RepositoryTagPage(values, next);

        // Act
        var resultValues = page.Values;
        var resultNext = page.Next;

        // Assert
        resultValues.Should().BeSameAs(values);
        resultNext.Should().BeSameAs(next);
    }

    [Fact(DisplayName = "RepositoryTagPage Empty returns empty page without next uri.")]
    [Trait("Category", "Unit")]
    public void EmptyReturnsEmptyPageWithoutNextUri()
    {
        // Arrange
        // Act
        var empty = RepositoryTagPage.Empty;

        // Assert
        empty.Values.Should().BeEmpty();
        empty.Next.Should().BeNull();
    }
}
