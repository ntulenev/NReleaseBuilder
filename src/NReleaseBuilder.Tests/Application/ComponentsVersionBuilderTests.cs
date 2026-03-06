using FluentAssertions;

using Moq;

using NuGet.Versioning;

using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Rendering;
using NReleaseBuilder.Bitbucket;

namespace NReleaseBuilder.Tests.Application;

public class ComponentsVersionBuilderTests
{
    [Fact(DisplayName = "ComponentsVersionBuilder can be created.")]
    [Trait("Category", "Unit")]
    public void ComponentsVersionBuilderCanBeCreated()
    {
        // Arrange
        var repositoryTagLookupBatchLoader = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict).Object;
        var versionChecker = new Mock<IComponentVersionChecker>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new ComponentsVersionBuilder(
            repositoryTagLookupBatchLoader,
            versionChecker));

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "ComponentsVersionBuilder cant be created with null repository loader.")]
    [Trait("Category", "Unit")]
    public void ComponentsVersionBuilderCantBeCreatedWithNullRepositoryLoader()
    {
        // Arrange
        var versionChecker = new Mock<IComponentVersionChecker>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new ComponentsVersionBuilder(
            null!,
            versionChecker));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "ComponentsVersionBuilder cant be created with null version checker.")]
    [Trait("Category", "Unit")]
    public void ComponentsVersionBuilderCantBeCreatedWithNullVersionChecker()
    {
        // Arrange
        var repositoryTagLookupBatchLoader = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new ComponentsVersionBuilder(
            repositoryTagLookupBatchLoader,
            null!));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "BuildAsync throws when component rows are null.")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncThrowsWhenComponentRowsAreNull()
    {
        // Arrange
        var repositoryTagLookupBatchLoader = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict).Object;
        var versionChecker = new Mock<IComponentVersionChecker>(MockBehavior.Strict).Object;
        var sut = new ComponentsVersionBuilder(repositoryTagLookupBatchLoader, versionChecker);

        // Act
        var action = () => sut.BuildAsync(null!, RepositoryVersionContext.BuildRepositoryVersionContext([]), CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "BuildAsync throws when repository context is null.")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncThrowsWhenRepositoryContextIsNull()
    {
        // Arrange
        var repositoryTagLookupBatchLoader = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict).Object;
        var versionChecker = new Mock<IComponentVersionChecker>(MockBehavior.Strict).Object;
        var sut = new ComponentsVersionBuilder(repositoryTagLookupBatchLoader, versionChecker);

        // Act
        var action = () => sut.BuildAsync([], null!, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "BuildAsync returns null when repository tag lookup loading fails.")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncReturnsNullWhenRepositoryTagLookupLoadingFails()
    {
        // Arrange
        var normalizedComponentRows = new List<ComponentRow>
        {
            Component("api", "repo-api", "1.0.0"),
            Component("worker", "repo-api", "0.9.0"),
            Component("gateway", "repo-gateway", "2.0.0"),
        };
        var repositoryContext = RepositoryVersionContext.BuildRepositoryVersionContext(normalizedComponentRows);
        var expectedRepositoryApi = new RepositoryName("repo-api");
        var expectedRepositoryGateway = new RepositoryName("repo-gateway");
        var expectedApiMinVersion = NuGetVersion.Parse("0.9.0");
        var expectedGatewayMinVersion = NuGetVersion.Parse("2.0.0");
        using var cts = new CancellationTokenSource();

        var repositoryTagLookupBatchLoaderMock = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict);
        repositoryTagLookupBatchLoaderMock
            .Setup(x => x.LoadAsync(
                It.Is<IReadOnlyList<RepositoryName>>(repositories =>
                    repositories.Count == 2
                    && repositories[0] == expectedRepositoryApi
                    && repositories[1] == expectedRepositoryGateway),
                It.Is<IReadOnlyDictionary<RepositoryName, NuGetVersion>>(minVersions =>
                    minVersions.Count == 2
                    && minVersions.ContainsKey(expectedRepositoryApi)
                    && minVersions[expectedRepositoryApi] == expectedApiMinVersion
                    && minVersions.ContainsKey(expectedRepositoryGateway)
                    && minVersions[expectedRepositoryGateway] == expectedGatewayMinVersion),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync((Dictionary<RepositoryName, RepositoryTagLookup>?)null);

        var versionCheckerMock = new Mock<IComponentVersionChecker>(MockBehavior.Strict);

        var sut = new ComponentsVersionBuilder(
            repositoryTagLookupBatchLoaderMock.Object,
            versionCheckerMock.Object);

        // Act
        var result = await sut.BuildAsync(normalizedComponentRows, repositoryContext, cts.Token);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "BuildAsync builds rows on success.")]
    [Trait("Category", "Unit")]
    public async Task BuildAsyncBuildsRowsOnSuccess()
    {
        // Arrange
        var normalizedComponentRows = new List<ComponentRow>
        {
            Component("api", "repo-api", "1.4.0"),
            Component("worker", "repo-api", "1.2.5"),
            Component("gateway", "repo-gateway", "v2.3.0"),
            Component("jobs", "repo-gateway", "not-a-version"),
        };
        var repositoryContext = RepositoryVersionContext.BuildRepositoryVersionContext(normalizedComponentRows);
        var expectedRepositoryApi = new RepositoryName("repo-api");
        var expectedRepositoryGateway = new RepositoryName("repo-gateway");
        var expectedApiMinVersion = NuGetVersion.Parse("1.2.5");
        var expectedGatewayMinVersion = NuGetVersion.Parse("2.3.0");
        var tagLookups = new Dictionary<RepositoryName, RepositoryTagLookup>
        {
            [expectedRepositoryApi] = RepositoryTagLookup.Success(expectedRepositoryApi, []),
            [expectedRepositoryGateway] = RepositoryTagLookup.Success(expectedRepositoryGateway, []),
        };
        IReadOnlyList<ComponentCheckRow> expectedRows =
        [
            new ComponentCheckRow(
                new ComponentCheckIndex(1),
                new ComponentName("api"),
                expectedRepositoryApi,
                new VersionLabel("1.4.0"),
                CheckStatus.UpToDate,
                new RowDetails("Already up to date"),
                []),
        ];
        using var cts = new CancellationTokenSource();

        var repositoryTagLookupBatchLoaderMock = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict);
        repositoryTagLookupBatchLoaderMock
            .Setup(x => x.LoadAsync(
                It.Is<IReadOnlyList<RepositoryName>>(repositories =>
                    repositories.Count == 2
                    && repositories[0] == expectedRepositoryApi
                    && repositories[1] == expectedRepositoryGateway),
                It.Is<IReadOnlyDictionary<RepositoryName, NuGetVersion>>(minVersions =>
                    minVersions.Count == 2
                    && minVersions.ContainsKey(expectedRepositoryApi)
                    && minVersions[expectedRepositoryApi] == expectedApiMinVersion
                    && minVersions.ContainsKey(expectedRepositoryGateway)
                    && minVersions[expectedRepositoryGateway] == expectedGatewayMinVersion),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync(tagLookups);

        var versionCheckerMock = new Mock<IComponentVersionChecker>(MockBehavior.Strict);
        versionCheckerMock
            .Setup(x => x.BuildRows(
                It.Is<IReadOnlyList<ComponentRow>>(rows =>
                    rows.Count == normalizedComponentRows.Count),
                It.Is<IReadOnlyDictionary<RepositoryName, RepositoryTagLookup>>(lookups =>
                    ReferenceEquals(lookups, tagLookups))))
            .Returns(expectedRows);

        var sut = new ComponentsVersionBuilder(
            repositoryTagLookupBatchLoaderMock.Object,
            versionCheckerMock.Object);

        // Act
        var result = await sut.BuildAsync(normalizedComponentRows, repositoryContext, cts.Token);

        // Assert
        result.Should().BeSameAs(expectedRows);
    }

    private static ComponentRow Component(string component, string repository, string version) =>
        new(
            new ComponentName(component),
            new RepositoryName(repository),
            new VersionLabel(version));
}
