using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Abstractions.Jira;
using NReleaseBuilder.Bitbucket.Internal;
using NReleaseBuilder.Bitbucket.Internal.Models;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;

using NuGet.Versioning;

namespace NReleaseBuilder.Tests.Bitbucket.Internal;

public class BitbucketTagLookupCoreTests
{
    [Fact(DisplayName = "BitbucketTagLookupCore constructor throws when options are null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenOptionsAreNull()
    {
        // Arrange
        var integrationCore = new Mock<IBitbucketIntegrationCore>(MockBehavior.Strict).Object;
        var jiraTaskResolver = new Mock<IJiraTaskResolver>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new BitbucketTagLookupCore(null!, integrationCore, jiraTaskResolver));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "BitbucketTagLookupCore constructor throws when integration core is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenIntegrationCoreIsNull()
    {
        // Arrange
        var options = CreateOptions(useTruncatedFallback: false);
        var jiraTaskResolver = new Mock<IJiraTaskResolver>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new BitbucketTagLookupCore(options, null!, jiraTaskResolver));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "BitbucketTagLookupCore constructor throws when jira task resolver is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenJiraTaskResolverIsNull()
    {
        // Arrange
        var options = CreateOptions(useTruncatedFallback: false);
        var integrationCore = new Mock<IBitbucketIntegrationCore>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new BitbucketTagLookupCore(options, integrationCore, null!));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "BitbucketTagLookupCore GetRepositoryTagLookupAsync returns repository-not-found lookup when repository is missing.")]
    [Trait("Category", "Unit")]
    public async Task GetRepositoryTagLookupAsyncReturnsRepositoryNotFoundLookupWhenRepositoryIsMissing()
    {
        // Arrange
        var options = CreateOptions(useTruncatedFallback: false);
        var repository = new RepositoryName("service.api");
        using var cts = new CancellationTokenSource();
        var integrationCallCount = 0;

        var integrationCoreMock = new Mock<IBitbucketIntegrationCore>(MockBehavior.Strict);
        integrationCoreMock
            .Setup(x => x.LoadRepositoryTagReferencesAsync(
                It.Is<RepositoryName>(value => value == repository),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => integrationCallCount++)
            .ReturnsAsync(RepositoryTagReferenceLoadResult.RepoNotFound());

        var jiraTaskResolverMock = new Mock<IJiraTaskResolver>(MockBehavior.Strict);
        var sut = new BitbucketTagLookupCore(options, integrationCoreMock.Object, jiraTaskResolverMock.Object);

        // Act
        var result = await sut.GetRepositoryTagLookupAsync(
            repository,
            minCurrentVersion: null,
            progress: null,
            cancellationToken: cts.Token);

        // Assert
        integrationCallCount.Should().Be(1);
        result.IsRepositoryMissing.Should().BeTrue();
        result.ResolvedRepository.Should().Be(repository);
        result.Error.Should().BeNull();
        result.Tags.Should().BeEmpty();
    }

    [Fact(DisplayName = "BitbucketTagLookupCore GetRepositoryTagLookupAsync uses truncated fallback when configured.")]
    [Trait("Category", "Unit")]
    public async Task GetRepositoryTagLookupAsyncUsesTruncatedFallbackWhenConfigured()
    {
        // Arrange
        var options = CreateOptions(useTruncatedFallback: true);
        var repository = new RepositoryName("service.api");
        var truncatedRepository = new RepositoryName("service");
        using var cts = new CancellationTokenSource();
        var integrationCallCount = 0;

        var integrationCoreMock = new Mock<IBitbucketIntegrationCore>(MockBehavior.Strict);
        integrationCoreMock
            .Setup(x => x.LoadRepositoryTagReferencesAsync(
                It.Is<RepositoryName>(value => value == repository),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => integrationCallCount++)
            .ReturnsAsync(RepositoryTagReferenceLoadResult.RepoNotFound());
        integrationCoreMock
            .Setup(x => x.LoadRepositoryTagReferencesAsync(
                It.Is<RepositoryName>(value => value == truncatedRepository),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => integrationCallCount++)
            .ReturnsAsync(RepositoryTagReferenceLoadResult.Success([]));

        var jiraTaskResolverMock = new Mock<IJiraTaskResolver>(MockBehavior.Strict);
        var sut = new BitbucketTagLookupCore(options, integrationCoreMock.Object, jiraTaskResolverMock.Object);

        // Act
        var result = await sut.GetRepositoryTagLookupAsync(
            repository,
            minCurrentVersion: null,
            progress: null,
            cancellationToken: cts.Token);

        // Assert
        integrationCallCount.Should().Be(2);
        result.IsRepositoryMissing.Should().BeFalse();
        result.Error.Should().BeNull();
        result.ResolvedRepository.Should().Be(truncatedRepository);
        result.Tags.Should().BeEmpty();
    }

    [Fact(DisplayName = "BitbucketTagLookupCore GetRepositoryTagLookupAsync returns api error lookup.")]
    [Trait("Category", "Unit")]
    public async Task GetRepositoryTagLookupAsyncReturnsApiErrorLookup()
    {
        // Arrange
        var options = CreateOptions(useTruncatedFallback: false);
        var repository = new RepositoryName("service.api");
        using var cts = new CancellationTokenSource();
        const string apiError = "bitbucket api failed";

        var integrationCoreMock = new Mock<IBitbucketIntegrationCore>(MockBehavior.Strict);
        integrationCoreMock
            .Setup(x => x.LoadRepositoryTagReferencesAsync(
                It.Is<RepositoryName>(value => value == repository),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(RepositoryTagReferenceLoadResult.ApiError(apiError));

        var jiraTaskResolverMock = new Mock<IJiraTaskResolver>(MockBehavior.Strict);
        var sut = new BitbucketTagLookupCore(options, integrationCoreMock.Object, jiraTaskResolverMock.Object);

        // Act
        var result = await sut.GetRepositoryTagLookupAsync(
            repository,
            minCurrentVersion: null,
            progress: null,
            cancellationToken: cts.Token);

        // Assert
        result.IsRepositoryMissing.Should().BeFalse();
        result.Error.Should().Be(apiError);
        result.ResolvedRepository.Should().Be(repository);
        result.Tags.Should().BeEmpty();
    }

    [Fact(DisplayName = "BitbucketTagLookupCore GetRepositoryTagLookupAsync builds enriched tags, filters by version and uses commit cache.")]
    [Trait("Category", "Unit")]
    public async Task GetRepositoryTagLookupAsyncBuildsEnrichedTagsFiltersByVersionAndUsesCommitCache()
    {
        // Arrange
        var options = CreateOptions(useTruncatedFallback: false);
        var repository = new RepositoryName("service.api");
        var commitHash = new CommitHash("abc123");
        var projectName = new JiraProjectName("PROJ");
        IReadOnlyList<RepositoryTagReference> tags =
        [
            new RepositoryTagReference(new VersionLabel("1.1.0"), commitHash),
            new RepositoryTagReference(new VersionLabel("1.2.0"), commitHash),
            new RepositoryTagReference(new VersionLabel("1.3.0"), null),
            new RepositoryTagReference(new VersionLabel("0.9.0"), new CommitHash("old")),
            new RepositoryTagReference(new VersionLabel("invalid"), new CommitHash("ignored")),
        ];
        var minCurrentVersion = NuGetVersion.Parse("1.0.0");
        var commitInfo = new CommitInfo("PROJ-1 fix bug");
        var jiraResolution = new JiraTaskResolution(
            new JiraStatusReference("Done"),
            new JiraTaskReference("PROJ-1"),
            new JiraTitleReference("Fix bug"),
            [],
            hasRequiredActions: true,
            hasBreakingChanges: false,
            hasDependencyIssues: false);
        using var cts = new CancellationTokenSource();
        var integrationLoadCallCount = 0;
        var commitMessageCallCount = 0;
        var resolveCallCount = 0;
        var progressTotalDetectedCount = 0;
        var progressProcessedCount = 0;
        var progress = new BitbucketProgressCallbacks
        {
            CommitTotalDetected = (_, count) =>
            {
                progressTotalDetectedCount++;
                count.Should().Be(3);
            },
            CommitProcessed = _ => progressProcessedCount++,
        };

        var integrationCoreMock = new Mock<IBitbucketIntegrationCore>(MockBehavior.Strict);
        integrationCoreMock
            .Setup(x => x.LoadRepositoryTagReferencesAsync(
                It.Is<RepositoryName>(value => value == repository),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => integrationLoadCallCount++)
            .ReturnsAsync(RepositoryTagReferenceLoadResult.Success(tags));
        integrationCoreMock
            .Setup(x => x.TryGetCommitMessageAsync(
                It.Is<RepositoryName>(value => value == repository),
                It.Is<CommitHash>(value => value == commitHash),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => commitMessageCallCount++)
            .ReturnsAsync(commitInfo);

        var jiraTaskResolverMock = new Mock<IJiraTaskResolver>(MockBehavior.Strict);
        jiraTaskResolverMock
            .Setup(x => x.ResolveFromCommitMessageAsync(
                It.Is<CommitInfo>(value => value.Message == "PROJ-1 fix bug"),
                It.Is<IReadOnlyList<JiraProjectName>>(value =>
                    value.Count == 1 && value[0] == projectName),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => resolveCallCount++)
            .ReturnsAsync(jiraResolution);

        var sut = new BitbucketTagLookupCore(options, integrationCoreMock.Object, jiraTaskResolverMock.Object);

        // Act
        var result = await sut.GetRepositoryTagLookupAsync(
            repository,
            minCurrentVersion,
            progress,
            cts.Token);

        // Assert
        integrationLoadCallCount.Should().Be(1);
        commitMessageCallCount.Should().Be(1);
        resolveCallCount.Should().Be(1);
        progressTotalDetectedCount.Should().Be(1);
        progressProcessedCount.Should().Be(3);

        result.IsRepositoryMissing.Should().BeFalse();
        result.Error.Should().BeNull();
        result.ResolvedRepository.Should().Be(repository);
        result.Tags.Should().HaveCount(3);
        result.Tags[0].Name.Value.Should().Be("1.1.0");
        result.Tags[0].JiraTask.Value.Should().Be("PROJ-1");
        result.Tags[1].Name.Value.Should().Be("1.2.0");
        result.Tags[1].JiraTask.Value.Should().Be("PROJ-1");
        result.Tags[2].Name.Value.Should().Be("1.3.0");
        result.Tags[2].JiraTask.Value.Should().Be("N/A");
        result.Tags[2].JiraTitle.Value.Should().Be("N/A");
        result.Tags[2].JiraStatus.Value.Should().Be("N/A");
    }

    private static IOptions<AppSettings> CreateOptions(bool useTruncatedFallback)
    {
        var settings = new AppSettings
        {
            CsvFilePath = "components.csv",
            Bitbucket = new BitbucketOptions
            {
                BaseUrl = new Uri("https://bitbucket.example.test/"),
                Workspace = "workspace",
                ProjectNames = ["PROJ"],
                AuthEmail = "bot@example.test",
                AuthApiToken = "token",
                UseTruncatedRepositoryNameFallback = useTruncatedFallback,
            },
            Jira = new JiraOptions
            {
                BaseUrl = new Uri("https://jira.example.test/"),
            },
        };

        return Options.Create(settings);
    }
}
