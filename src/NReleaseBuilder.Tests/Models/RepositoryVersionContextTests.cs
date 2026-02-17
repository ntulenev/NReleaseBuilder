using NReleaseBuilder.Models;

namespace NReleaseBuilder.Tests.Models;

public sealed class RepositoryVersionContextTests
{
    [Fact]
    public void BuildRepositoryVersionContextWithNullRowsThrowsArgumentNullException()
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var act = () => RepositoryVersionContext.BuildRepositoryVersionContext(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        _ = Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void BuildRepositoryVersionContextReturnsDistinctRepositoriesCaseInsensitive()
    {
        var componentRows = new[]
        {
            BuildRow("component-a", "repo-a", "1.0.0"),
            BuildRow("component-b", "REPO-A", "1.1.0"),
            BuildRow("component-c", "repo-b", "2.0.0"),
        };

        var result = RepositoryVersionContext.BuildRepositoryVersionContext(componentRows);

        Assert.Equal(2, result.Repositories.Count);
        Assert.Contains(new RepositoryName("repo-a"), result.Repositories);
        Assert.Contains(new RepositoryName("repo-b"), result.Repositories);
    }

    [Fact]
    public void BuildRepositoryVersionContextBuildsMinCurrentVersionsByRepository()
    {
        var componentRows = new[]
        {
            BuildRow("component-a", "repo-a", "1.5.0"),
            BuildRow("component-b", "repo-a", "1.2.0"),
            BuildRow("component-c", "repo-a", "2.0.0"),
            BuildRow("component-d", "repo-b", "v3.1.0"),
            BuildRow("component-e", "repo-b", "3.0.9"),
        };

        var result = RepositoryVersionContext.BuildRepositoryVersionContext(componentRows);

        Assert.Equal(2, result.MinCurrentVersionsByRepository.Count);
        Assert.Equal("1.2.0", result.MinCurrentVersionsByRepository[new RepositoryName("repo-a")].ToNormalizedString());
        Assert.Equal("3.0.9", result.MinCurrentVersionsByRepository[new RepositoryName("repo-b")].ToNormalizedString());
    }

    [Fact]
    public void BuildRepositoryVersionContextSkipsInvalidVersionsInMinVersionMap()
    {
        var componentRows = new[]
        {
            BuildRow("component-a", "repo-a", "not-a-version"),
            BuildRow("component-b", "repo-b", "2.0.0"),
        };

        var result = RepositoryVersionContext.BuildRepositoryVersionContext(componentRows);

        Assert.Contains(new RepositoryName("repo-a"), result.Repositories);
        Assert.DoesNotContain(new RepositoryName("repo-a"), result.MinCurrentVersionsByRepository.Keys);
        Assert.Equal("2.0.0", result.MinCurrentVersionsByRepository[new RepositoryName("repo-b")].ToNormalizedString());
    }

    private static ComponentRow BuildRow(string componentName, string repositoryName, string versionLabel)
        => new(
            new ComponentName(componentName),
            new RepositoryName(repositoryName),
            new VersionLabel(versionLabel));
}
