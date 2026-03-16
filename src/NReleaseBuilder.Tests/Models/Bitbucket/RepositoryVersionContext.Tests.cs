using FluentAssertions;

using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;

using NuGet.Versioning;

namespace NReleaseBuilder.Tests.Models.Bitbucket;

public class RepositoryVersionContextTests
{
    [Fact(DisplayName = "RepositoryVersionContext BuildRepositoryVersionContext throws when rows are null.")]
    [Trait("Category", "Unit")]
    public void BuildRepositoryVersionContextThrowsWhenRowsAreNull()
    {
        // Arrange
        // Act
        Action action = () => _ = RepositoryVersionContext.BuildRepositoryVersionContext(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("componentRows");
    }

    [Fact(DisplayName = "RepositoryVersionContext BuildRepositoryVersionContext builds distinct repositories and min versions.")]
    [Trait("Category", "Unit")]
    public void BuildRepositoryVersionContextBuildsDistinctRepositoriesAndMinVersions()
    {
        // Arrange
        IReadOnlyList<ComponentRow> rows =
        [
            Row("component-a", "repo-a", "2.0.0"),
            Row("component-b", "repo-a", "1.5.0"),
            Row("component-c", "repo-b", "v3.1.0"),
            Row("component-d", "repo-b", "invalid"),
            Row("component-e", "repo-c", "invalid"),
        ];

        // Act
        var context = RepositoryVersionContext.BuildRepositoryVersionContext(rows);

        // Assert
        context.Repositories.Should().HaveCount(3);
        context.Repositories.Should().ContainInOrder(
            new RepositoryName("repo-a"),
            new RepositoryName("repo-b"),
            new RepositoryName("repo-c"));

        context.MinCurrentVersionsByRepository.Should().HaveCount(2);
        context.MinCurrentVersionsByRepository[new RepositoryName("repo-a")]
            .Should().Be(NuGetVersion.Parse("1.5.0"));
        context.MinCurrentVersionsByRepository[new RepositoryName("repo-b")]
            .Should().Be(NuGetVersion.Parse("3.1.0"));
        context.MinCurrentVersionsByRepository.ContainsKey(new RepositoryName("repo-c"))
            .Should().BeFalse();
    }

    [Fact(DisplayName = "RepositoryVersionContext omits min version when repository has unreleased components.")]
    [Trait("Category", "Unit")]
    public void BuildRepositoryVersionContextOmitsMinVersionWhenRepositoryHasUnreleasedComponents()
    {
        // Arrange
        IReadOnlyList<ComponentRow> rows =
        [
            Row("component-a", "repo-a", "2.0.0"),
            Row("component-b", "repo-a", "3.0.0", isReleased: false),
            Row("component-c", "repo-b", "1.0.0"),
        ];

        // Act
        var context = RepositoryVersionContext.BuildRepositoryVersionContext(rows);

        // Assert
        context.MinCurrentVersionsByRepository.ContainsKey(new RepositoryName("repo-a"))
            .Should().BeFalse();
        context.MinCurrentVersionsByRepository[new RepositoryName("repo-b")]
            .Should().Be(NuGetVersion.Parse("1.0.0"));
    }

    private static ComponentRow Row(string component, string repository, string version, bool isReleased = true) =>
        new(
            new ComponentName(component),
            new RepositoryName(repository),
            new VersionLabel(version),
            isReleased);
}
