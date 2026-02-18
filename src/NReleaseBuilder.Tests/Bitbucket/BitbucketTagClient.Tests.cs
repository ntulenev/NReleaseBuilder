using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Bitbucket;

using NuGet.Versioning;

namespace NReleaseBuilder.Tests.Bitbucket;

public class BitbucketTagClientTests
{
    [Fact(DisplayName = "BitbucketTagClient can be created.")]
    [Trait("Category", "Unit")]
    public void BitbucketTagClientCanBeCreated()
    {
        // Arrange
        var options = CreateOptions(maxParallelRequests: 2);
        var core = new Mock<IBitbucketTagLookupCore>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new BitbucketTagClient(options, core));

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "BitbucketTagClient constructor throws when options are null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenOptionsAreNull()
    {
        // Arrange
        var core = new Mock<IBitbucketTagLookupCore>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new BitbucketTagClient(null!, core));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "BitbucketTagClient constructor throws when lookup core is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenLookupCoreIsNull()
    {
        // Arrange
        var options = CreateOptions(maxParallelRequests: 2);

        // Act
        var exception = Record.Exception(() => new BitbucketTagClient(options, null!));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "BitbucketTagClient FetchRepositoryTagLookupsAsync throws when repositories are null.")]
    [Trait("Category", "Unit")]
    public async Task FetchRepositoryTagLookupsAsyncThrowsWhenRepositoriesAreNull()
    {
        // Arrange
        var options = CreateOptions(maxParallelRequests: 2);
        var core = new Mock<IBitbucketTagLookupCore>(MockBehavior.Strict).Object;
        var sut = new BitbucketTagClient(options, core);
        IReadOnlyDictionary<RepositoryName, NuGetVersion> minVersions = new Dictionary<RepositoryName, NuGetVersion>();

        // Act
        Func<Task> action = () => sut.FetchRepositoryTagLookupsAsync(
            null!,
            minVersions,
            progress: null,
            cancellationToken: CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("repositories");
    }

    [Fact(DisplayName = "BitbucketTagClient FetchRepositoryTagLookupsAsync throws when min versions are null.")]
    [Trait("Category", "Unit")]
    public async Task FetchRepositoryTagLookupsAsyncThrowsWhenMinVersionsAreNull()
    {
        // Arrange
        var options = CreateOptions(maxParallelRequests: 2);
        var core = new Mock<IBitbucketTagLookupCore>(MockBehavior.Strict).Object;
        var sut = new BitbucketTagClient(options, core);
        IReadOnlyList<RepositoryName> repositories = [new RepositoryName("repo-a")];

        // Act
        Func<Task> action = () => sut.FetchRepositoryTagLookupsAsync(
            repositories,
            null!,
            progress: null,
            cancellationToken: CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("minCurrentVersionsByRepository");
    }

    [Fact(DisplayName = "BitbucketTagClient FetchRepositoryTagLookupsAsync loads repositories and reports progress.")]
    [Trait("Category", "Unit")]
    public async Task FetchRepositoryTagLookupsAsyncLoadsRepositoriesAndReportsProgress()
    {
        // Arrange
        var options = CreateOptions(maxParallelRequests: 1);
        var repositoryA = new RepositoryName("repo-a");
        var repositoryB = new RepositoryName("repo-b");
        IReadOnlyList<RepositoryName> repositories = [repositoryA, repositoryB];
        var minVersionForRepositoryA = NuGetVersion.Parse("1.0.0");
        IReadOnlyDictionary<RepositoryName, NuGetVersion> minVersions =
            new Dictionary<RepositoryName, NuGetVersion>
            {
                [repositoryA] = minVersionForRepositoryA,
            };

        var lookupA = RepositoryTagLookup.Success(repositoryA, []);
        var lookupB = RepositoryTagLookup.Success(repositoryB, []);
        using var cts = new CancellationTokenSource();
        var coreCallCount = 0;
        var progressStarted = new List<string>();
        var progressCompleted = new List<string>();
        var progress = new BitbucketProgressCallbacks
        {
            RepositoryStarted = name => progressStarted.Add(name),
            RepositoryCompleted = name => progressCompleted.Add(name),
        };

        var coreMock = new Mock<IBitbucketTagLookupCore>(MockBehavior.Strict);
        coreMock
            .Setup(x => x.GetRepositoryTagLookupAsync(
                It.Is<RepositoryName>(value => value == repositoryA),
                It.Is<NuGetVersion?>(value => value == minVersionForRepositoryA),
                It.Is<BitbucketProgressCallbacks?>(value => ReferenceEquals(value, progress)),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => coreCallCount++)
            .ReturnsAsync(lookupA);
        coreMock
            .Setup(x => x.GetRepositoryTagLookupAsync(
                It.Is<RepositoryName>(value => value == repositoryB),
                It.Is<NuGetVersion?>(value => value == null),
                It.Is<BitbucketProgressCallbacks?>(value => ReferenceEquals(value, progress)),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => coreCallCount++)
            .ReturnsAsync(lookupB);

        var sut = new BitbucketTagClient(options, coreMock.Object);

        // Act
        var result = await sut.FetchRepositoryTagLookupsAsync(
            repositories,
            minVersions,
            progress,
            cts.Token);

        // Assert
        coreCallCount.Should().Be(2);
        result.Should().HaveCount(2);
        result[repositoryA].Should().Be(lookupA);
        result[repositoryB].Should().Be(lookupB);
        progressStarted.Should().BeEquivalentTo(["repo-a", "repo-b"], options => options.WithStrictOrdering());
        progressCompleted.Should().BeEquivalentTo(["repo-a", "repo-b"], options => options.WithStrictOrdering());
    }

    private static IOptions<AppSettings> CreateOptions(int maxParallelRequests)
    {
        var settings = new AppSettings
        {
            CsvFilePath = "components.csv",
            Bitbucket = new BitbucketOptions
            {
                BaseUrl = new Uri("https://bitbucket.example.test/"),
                Workspace = "workspace",
                ProjectNames = [new JiraProjectName("PROJ").Value],
                AuthEmail = "bot@example.test",
                AuthApiToken = "token",
                MaxParallelRequests = maxParallelRequests,
            },
            Jira = new JiraOptions
            {
                BaseUrl = new Uri("https://jira.example.test/"),
            },
        };

        return Options.Create(settings);
    }
}
