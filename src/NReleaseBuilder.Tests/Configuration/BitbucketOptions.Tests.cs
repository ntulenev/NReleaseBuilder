using FluentAssertions;

using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Configuration;

public class BitbucketOptionsTests
{
    [Fact(DisplayName = "BitbucketOptions ResolveProjectNames uses ProjectNames with trim and distinct behavior.")]
    [Trait("Category", "Unit")]
    public void ResolveProjectNamesUsesProjectNamesWithTrimAndDistinctBehavior()
    {
        // Arrange
        var options = CreateOptions(
            projectNames: ["  PROJ  ", "proj", "OPS"],
            projectName: "LEGACY");

        // Act
        var result = options.ResolveProjectNames();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(new JiraProjectName("PROJ"));
        result.Should().Contain(new JiraProjectName("OPS"));
    }

    [Fact(DisplayName = "BitbucketOptions ResolveProjectNames falls back to ProjectName alias.")]
    [Trait("Category", "Unit")]
    public void ResolveProjectNamesFallsBackToProjectNameAlias()
    {
        // Arrange
        var options = CreateOptions(
            projectNames: [],
            projectName: "  PROJ  ");

        // Act
        var result = options.ResolveProjectNames();

        // Assert
        result.Should().Equal(new JiraProjectName("PROJ"));
    }

    [Fact(DisplayName = "BitbucketOptions ResolveProjectNames returns empty when both project sources are empty.")]
    [Trait("Category", "Unit")]
    public void ResolveProjectNamesReturnsEmptyWhenBothProjectSourcesAreEmpty()
    {
        // Arrange
        var options = CreateOptions(
            projectNames: [],
            projectName: " ");

        // Act
        var result = options.ResolveProjectNames();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "BitbucketOptions ResolveRepositoryName returns original when overrides are empty.")]
    [Trait("Category", "Unit")]
    public void ResolveRepositoryNameReturnsOriginalWhenOverridesAreEmpty()
    {
        // Arrange
        var options = CreateOptions();
        var repository = new RepositoryName("repo-api");

        // Act
        var result = options.ResolveRepositoryName(repository);

        // Assert
        result.Should().Be(repository);
    }

    [Fact(DisplayName = "BitbucketOptions ResolveRepositoryName applies override case-insensitively.")]
    [Trait("Category", "Unit")]
    public void ResolveRepositoryNameAppliesOverrideCaseInsensitively()
    {
        // Arrange
        var options = CreateOptions(
            repositoryNameOverrides: new Dictionary<string, string>
            {
                ["Repo-Api"] = "repo-api-renamed",
            });

        var repository = new RepositoryName("repo-api");

        // Act
        var result = options.ResolveRepositoryName(repository);

        // Assert
        result.Should().Be(new RepositoryName("repo-api-renamed"));
    }

    private static BitbucketOptions CreateOptions(
        IReadOnlyList<string>? projectNames = null,
        string? projectName = null,
        IReadOnlyDictionary<string, string>? repositoryNameOverrides = null) =>
        new()
        {
            BaseUrl = new Uri("https://bitbucket.example.test/"),
            Workspace = "workspace",
            ProjectNames = projectNames ?? ["PROJ"],
            ProjectName = projectName ?? string.Empty,
            AuthEmail = "bot@example.test",
            AuthApiToken = "token",
            RepositoryNameOverrides = repositoryNameOverrides ?? new Dictionary<string, string>(),
        };
}
