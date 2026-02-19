using FluentAssertions;

using NReleaseBuilder.Bitbucket;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Tests.Bitbucket;

public class ComponentVersionCheckerTests
{
    [Fact(DisplayName = "ComponentVersionChecker BuildRows throws when component rows are null.")]
    [Trait("Category", "Unit")]
    public void BuildRowsThrowsWhenComponentRowsAreNull()
    {
        // Arrange
        var sut = new ComponentVersionChecker();
        IReadOnlyDictionary<RepositoryName, RepositoryTagLookup> tagLookups =
            new Dictionary<RepositoryName, RepositoryTagLookup>();

        // Act
        Action action = () => _ = sut.BuildRows(null!, tagLookups);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("componentRows");
    }

    [Fact(DisplayName = "ComponentVersionChecker BuildRows throws when tag lookups are null.")]
    [Trait("Category", "Unit")]
    public void BuildRowsThrowsWhenTagLookupsAreNull()
    {
        // Arrange
        var sut = new ComponentVersionChecker();
        IReadOnlyList<ComponentRow> rows = [];

        // Act
        Action action = () => _ = sut.BuildRows(rows, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("tagLookups");
    }

    [Fact(DisplayName = "ComponentVersionChecker BuildRows maps all status branches.")]
    [Trait("Category", "Unit")]
    public void BuildRowsMapsAllStatusBranches()
    {
        // Arrange
        var sut = new ComponentVersionChecker();
        IReadOnlyList<ComponentRow> rows =
        [
            Component("missing-lookup-component", "repo-missing-lookup", "1.0.0"),
            Component("missing-repo-component", "repo-missing", "1.0.0"),
            Component("error-component", "repo-error", "1.0.0"),
            Component("invalid-version-component", "repo-invalid-current", "invalid"),
            Component("up-to-date-component", "repo-up-to-date", "1.2.0"),
            Component("outdated-component", "repo-outdated", "1.0.0"),
        ];

        IReadOnlyDictionary<RepositoryName, RepositoryTagLookup> lookups =
            new Dictionary<RepositoryName, RepositoryTagLookup>
            {
                [new RepositoryName("repo-missing")] =
                    RepositoryTagLookup.RepoNotFound(new RepositoryName("resolved-missing-repo")),
                [new RepositoryName("repo-error")] =
                    RepositoryTagLookup.ApiError(new RepositoryName("resolved-error-repo"), "Bitbucket failed"),
                [new RepositoryName("repo-invalid-current")] =
                    RepositoryTagLookup.Success(
                        new RepositoryName("resolved-invalid-current-repo"),
                        [CreateTag("2.0.0", "TASK-1", "Title", "Done")]),
                [new RepositoryName("repo-up-to-date")] =
                    RepositoryTagLookup.Success(
                        new RepositoryName("resolved-up-to-date-repo"),
                        [
                            CreateTag("1.2.0", "TASK-2", "Title", "Done"),
                            CreateTag("1.0.0", "TASK-3", "Title", "Done"),
                            CreateTag("not-a-version", "TASK-4", "Title", "Done"),
                        ]),
                [new RepositoryName("repo-outdated")] =
                    RepositoryTagLookup.Success(
                        new RepositoryName("resolved-outdated-repo"),
                        [
                            CreateTag("2.0.0", "TASK-20", "Second", "In Progress"),
                            CreateTag(
                                "1.1.0",
                                "TASK-10",
                                "First",
                                "Done",
                                new Uri("https://bitbucket.example.test/workspace/repo-outdated/pull-requests/10")),
                            CreateTag("invalid", "TASK-30", "Ignored", "Done"),
                        ]),
            };

        // Act
        var result = sut.BuildRows(rows, lookups);

        // Assert
        result.Should().HaveCount(6);

        result[0].Index.Value.Should().Be(1);
        result[0].Status.Should().Be(CheckStatus.BitbucketError);
        result[0].Repository.Value.Should().Be("repo-missing-lookup");
        result[0].DetailsMessage.Value.Should().Be("Repository lookup result is missing.");

        result[1].Index.Value.Should().Be(2);
        result[1].Status.Should().Be(CheckStatus.RepositoryNotFound);
        result[1].Repository.Value.Should().Be("resolved-missing-repo");
        result[1].DetailsMessage.Value.Should().Be("Repository was not found in Bitbucket workspace.");

        result[2].Index.Value.Should().Be(3);
        result[2].Status.Should().Be(CheckStatus.BitbucketError);
        result[2].Repository.Value.Should().Be("resolved-error-repo");
        result[2].DetailsMessage.Value.Should().Be("Bitbucket failed");

        result[3].Index.Value.Should().Be(4);
        result[3].Status.Should().Be(CheckStatus.InvalidCurrentVersion);
        result[3].Repository.Value.Should().Be("resolved-invalid-current-repo");
        result[3].DetailsMessage.Value.Should().Be("Current version is not a valid tag format.");

        result[4].Index.Value.Should().Be(5);
        result[4].Status.Should().Be(CheckStatus.UpToDate);
        result[4].Repository.Value.Should().Be("resolved-up-to-date-repo");
        result[4].NewerVersions.Should().BeEmpty();

        result[5].Index.Value.Should().Be(6);
        result[5].Status.Should().Be(CheckStatus.Outdated);
        result[5].Repository.Value.Should().Be("resolved-outdated-repo");
        result[5].NewerVersions.Should().HaveCount(2);
        result[5].NewerVersions[0].Version.Value.Should().Be("1.1.0");
        result[5].NewerVersions[0].JiraTask.Value.Should().Be("TASK-10");
        result[5].NewerVersions[0].PullRequestUrl.Should().Be(
            new Uri("https://bitbucket.example.test/workspace/repo-outdated/pull-requests/10"));
        result[5].NewerVersions[1].Version.Value.Should().Be("2.0.0");
        result[5].NewerVersions[1].JiraTask.Value.Should().Be("TASK-20");
        result[5].NewerVersions[1].PullRequestUrl.Should().BeNull();
    }

    private static ComponentRow Component(string component, string repository, string version) =>
        new(
            new ComponentName(component),
            new RepositoryName(repository),
            new VersionLabel(version));

    private static RepositoryTagInfo CreateTag(
        string version,
        string jiraTask,
        string jiraTitle,
        string jiraStatus,
        Uri? pullRequestUrl = null) =>
        new(
            new VersionLabel(version),
            new JiraTaskReference(jiraTask),
            new JiraTitleReference(jiraTitle),
            new JiraStatusReference(jiraStatus),
            [],
            hasRequiredActions: false,
            hasBreakingChanges: false,
            hasDependencyIssues: false,
            pullRequestUrl);
}
