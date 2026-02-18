using FluentAssertions;

using Moq;

using NuGet.Versioning;

using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Abstractions.Csv;
using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Application;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;

namespace NReleaseBuilder.Tests.Application;

public class VersionCheckApplicationTests
{
    [Fact(DisplayName = "VersionCheckApplication can be created.")]
    [Trait("Category", "Unit")]
    public void VersionCheckApplicationCanBeCreated()
    {
        // Arrange
        var csvReader = new Mock<ICsvComponentReader>(MockBehavior.Strict).Object;
        var repositoryLoader = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict).Object;
        var versionChecker = new Mock<IComponentVersionChecker>(MockBehavior.Strict).Object;
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new VersionCheckApplication(csvReader, repositoryLoader, versionChecker, renderer));

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "VersionCheckApplication cant be created with null csv reader.")]
    [Trait("Category", "Unit")]
    public void VersionCheckApplicationCantBeCreatedWithNullCsvReader()
    {
        // Arrange
        var repositoryLoader = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict).Object;
        var versionChecker = new Mock<IComponentVersionChecker>(MockBehavior.Strict).Object;
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new VersionCheckApplication(null!, repositoryLoader, versionChecker, renderer));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "VersionCheckApplication cant be created with null repository loader.")]
    [Trait("Category", "Unit")]
    public void VersionCheckApplicationCantBeCreatedWithNullRepositoryLoader()
    {
        // Arrange
        var csvReader = new Mock<ICsvComponentReader>(MockBehavior.Strict).Object;
        var versionChecker = new Mock<IComponentVersionChecker>(MockBehavior.Strict).Object;
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new VersionCheckApplication(csvReader, null!, versionChecker, renderer));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "VersionCheckApplication cant be created with null version checker.")]
    [Trait("Category", "Unit")]
    public void VersionCheckApplicationCantBeCreatedWithNullVersionChecker()
    {
        // Arrange
        var csvReader = new Mock<ICsvComponentReader>(MockBehavior.Strict).Object;
        var repositoryLoader = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict).Object;
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new VersionCheckApplication(csvReader, repositoryLoader, null!, renderer));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "VersionCheckApplication cant be created with null renderer.")]
    [Trait("Category", "Unit")]
    public void VersionCheckApplicationCantBeCreatedWithNullRenderer()
    {
        // Arrange
        var csvReader = new Mock<ICsvComponentReader>(MockBehavior.Strict).Object;
        var repositoryLoader = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict).Object;
        var versionChecker = new Mock<IComponentVersionChecker>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new VersionCheckApplication(csvReader, repositoryLoader, versionChecker, null!));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "RunAsync returns 1 when csv read fails.")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncReturnsOneWhenCsvReadFails()
    {
        // Arrange
        var csvReadCount = 0;
        var csvReaderMock = new Mock<ICsvComponentReader>(MockBehavior.Strict);
        csvReaderMock
            .Setup(x => x.Read())
            .Callback(() => csvReadCount++)
            .Returns((IReadOnlyList<ComponentRow>?)null);

        var repositoryLoaderMock = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict);
        var versionCheckerMock = new Mock<IComponentVersionChecker>(MockBehavior.Strict);
        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        var sut = new VersionCheckApplication(
            csvReaderMock.Object,
            repositoryLoaderMock.Object,
            versionCheckerMock.Object,
            rendererMock.Object);

        // Act
        var result = await sut.RunAsync(CancellationToken.None);

        // Assert
        result.Should().Be(1);
        csvReadCount.Should().Be(1);
    }

    [Fact(DisplayName = "RunAsync returns 0 and prints no rows when csv is empty.")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncReturnsZeroAndPrintsNoRowsWhenCsvIsEmpty()
    {
        // Arrange
        var csvReadCount = 0;
        var renderHeaderCount = 0;
        var printNoRowsCount = 0;
        var csvRows = new List<ComponentRow>();

        var csvReaderMock = new Mock<ICsvComponentReader>(MockBehavior.Strict);
        csvReaderMock
            .Setup(x => x.Read())
            .Callback(() => csvReadCount++)
            .Returns(csvRows);

        var repositoryLoaderMock = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict);
        var versionCheckerMock = new Mock<IComponentVersionChecker>(MockBehavior.Strict);
        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.RenderHeader())
            .Callback(() => renderHeaderCount++);
        rendererMock
            .Setup(x => x.PrintNoRows())
            .Callback(() => printNoRowsCount++);

        var sut = new VersionCheckApplication(
            csvReaderMock.Object,
            repositoryLoaderMock.Object,
            versionCheckerMock.Object,
            rendererMock.Object);

        // Act
        var result = await sut.RunAsync(CancellationToken.None);

        // Assert
        result.Should().Be(0);
        csvReadCount.Should().Be(1);
        renderHeaderCount.Should().Be(1);
        printNoRowsCount.Should().Be(1);
    }

    [Fact(DisplayName = "RunAsync returns 1 when repository tag lookup fails.")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncReturnsOneWhenRepositoryTagLookupFails()
    {
        // Arrange
        var componentRows = new List<ComponentRow>
        {
            Component("api", "repo-api", "1.0.0"),
            Component("worker", "repo-api", "0.9.0"),
            Component("gateway", "repo-gateway", "2.0.0"),
        };

        var csvReadCount = 0;
        var renderHeaderCount = 0;
        var repositoryCheckCountCalls = 0;
        var loaderCallCount = 0;
        IReadOnlyList<RepositoryName>? capturedRepositories = null;
        IReadOnlyDictionary<RepositoryName, NuGetVersion>? capturedMinVersions = null;
        var capturedToken = CancellationToken.None;
        using var cts = new CancellationTokenSource();
        var expectedRepositoryApi = new RepositoryName("repo-api");
        var expectedRepositoryGateway = new RepositoryName("repo-gateway");
        var expectedApiMinVersion = NuGetVersion.Parse("0.9.0");
        var expectedGatewayMinVersion = NuGetVersion.Parse("2.0.0");

        var csvReaderMock = new Mock<ICsvComponentReader>(MockBehavior.Strict);
        csvReaderMock
            .Setup(x => x.Read())
            .Callback(() => csvReadCount++)
            .Returns(componentRows);

        var repositoryLoaderMock = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict);
        repositoryLoaderMock
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
            .Callback<IReadOnlyList<RepositoryName>, IReadOnlyDictionary<RepositoryName, NuGetVersion>, CancellationToken>(
                (repositories, minVersions, token) =>
                {
                    loaderCallCount++;
                    capturedRepositories = repositories;
                    capturedMinVersions = minVersions;
                    capturedToken = token;
                })
            .ReturnsAsync((Dictionary<RepositoryName, RepositoryTagLookup>?)null);

        var versionCheckerMock = new Mock<IComponentVersionChecker>(MockBehavior.Strict);
        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.RenderHeader())
            .Callback(() => renderHeaderCount++);
        rendererMock
            .Setup(x => x.PrintRepositoryCheckCount(It.Is<int>(count => count == 2)))
            .Callback<int>(count => repositoryCheckCountCalls++);

        var sut = new VersionCheckApplication(
            csvReaderMock.Object,
            repositoryLoaderMock.Object,
            versionCheckerMock.Object,
            rendererMock.Object);

        // Act
        var result = await sut.RunAsync(cts.Token);

        // Assert
        result.Should().Be(1);
        csvReadCount.Should().Be(1);
        renderHeaderCount.Should().Be(1);
        repositoryCheckCountCalls.Should().Be(1);
        loaderCallCount.Should().Be(1);
        capturedRepositories.Should().NotBeNull();
        capturedRepositories.Should().Equal(expectedRepositoryApi, expectedRepositoryGateway);
        var minVersions = capturedMinVersions ?? throw new InvalidOperationException("Expected min versions to be captured.");
        minVersions.Should().HaveCount(2);
        minVersions[expectedRepositoryApi].Should().Be(expectedApiMinVersion);
        minVersions[expectedRepositoryGateway].Should().Be(expectedGatewayMinVersion);
        capturedToken.Should().Be(cts.Token);
    }

    [Fact(DisplayName = "RunAsync returns 0 and renders results on successful flow.")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncReturnsZeroAndRendersResultsOnSuccessfulFlow()
    {
        // Arrange
        var componentRows = new List<ComponentRow>
        {
            Component("api", "repo-api", "1.4.0"),
            Component("worker", "repo-api", "1.2.5"),
            Component("gateway", "repo-gateway", "v2.3.0"),
            Component("jobs", "repo-gateway", "not-a-version"),
        };
        var tagLookups = new Dictionary<RepositoryName, RepositoryTagLookup>
        {
            [new RepositoryName("repo-api")] = RepositoryTagLookup.Success(new RepositoryName("repo-api"), []),
            [new RepositoryName("repo-gateway")] = RepositoryTagLookup.Success(new RepositoryName("repo-gateway"), []),
        };
        IReadOnlyList<ComponentCheckRow> expectedRows =
        [
            new ComponentCheckRow(
                new ComponentCheckIndex(1),
                new ComponentName("api"),
                new RepositoryName("repo-api"),
                new VersionLabel("1.4.0"),
                CheckStatus.UpToDate,
                new RowDetails("Already up to date"),
                []),
        ];

        var csvReadCount = 0;
        var renderHeaderCount = 0;
        var repositoryCheckCountCalls = 0;
        var loaderCallCount = 0;
        IReadOnlyList<RepositoryName>? capturedRepositories = null;
        IReadOnlyDictionary<RepositoryName, NuGetVersion>? capturedMinVersions = null;
        var capturedLoaderToken = CancellationToken.None;
        var checkerCallCount = 0;
        IReadOnlyList<ComponentRow>? capturedRowsForChecker = null;
        IReadOnlyDictionary<RepositoryName, RepositoryTagLookup>? capturedLookupsForChecker = null;
        var renderResultsCount = 0;
        IReadOnlyList<ComponentCheckRow>? capturedRowsForRenderer = null;
        using var cts = new CancellationTokenSource();
        var expectedRepositoryApi = new RepositoryName("repo-api");
        var expectedRepositoryGateway = new RepositoryName("repo-gateway");
        var expectedApiMinVersion = NuGetVersion.Parse("1.2.5");
        var expectedGatewayMinVersion = NuGetVersion.Parse("2.3.0");

        var csvReaderMock = new Mock<ICsvComponentReader>(MockBehavior.Strict);
        csvReaderMock
            .Setup(x => x.Read())
            .Callback(() => csvReadCount++)
            .Returns(componentRows);

        var repositoryLoaderMock = new Mock<IRepositoryTagLookupBatchLoader>(MockBehavior.Strict);
        repositoryLoaderMock
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
            .Callback<IReadOnlyList<RepositoryName>, IReadOnlyDictionary<RepositoryName, NuGetVersion>, CancellationToken>(
                (repositories, minVersions, token) =>
                {
                    loaderCallCount++;
                    capturedRepositories = repositories;
                    capturedMinVersions = minVersions;
                    capturedLoaderToken = token;
                })
            .ReturnsAsync(tagLookups);

        var versionCheckerMock = new Mock<IComponentVersionChecker>(MockBehavior.Strict);
        versionCheckerMock
            .Setup(x => x.BuildRows(
                It.Is<IReadOnlyList<ComponentRow>>(rows =>
                    rows.Count == componentRows.Count
                    && rows[0].Component == componentRows[0].Component
                    && rows[0].Repository == componentRows[0].Repository
                    && rows[0].Version == componentRows[0].Version
                    && rows[1].Component == componentRows[1].Component
                    && rows[1].Repository == componentRows[1].Repository
                    && rows[1].Version == componentRows[1].Version
                    && rows[2].Component == componentRows[2].Component
                    && rows[2].Repository == componentRows[2].Repository
                    && rows[2].Version == componentRows[2].Version
                    && rows[3].Component == componentRows[3].Component
                    && rows[3].Repository == componentRows[3].Repository
                    && rows[3].Version == componentRows[3].Version),
                It.Is<IReadOnlyDictionary<RepositoryName, RepositoryTagLookup>>(lookups =>
                    ReferenceEquals(lookups, tagLookups))))
            .Callback<IReadOnlyList<ComponentRow>, IReadOnlyDictionary<RepositoryName, RepositoryTagLookup>>(
                (rows, lookups) =>
                {
                    checkerCallCount++;
                    capturedRowsForChecker = rows;
                    capturedLookupsForChecker = lookups;
                })
            .Returns(expectedRows);

        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.RenderHeader())
            .Callback(() => renderHeaderCount++);
        rendererMock
            .Setup(x => x.PrintRepositoryCheckCount(It.Is<int>(count => count == 2)))
            .Callback<int>(count => repositoryCheckCountCalls++);
        rendererMock
            .Setup(x => x.RenderResults(It.Is<IReadOnlyList<ComponentCheckRow>>(rows => ReferenceEquals(rows, expectedRows))))
            .Callback<IReadOnlyList<ComponentCheckRow>>(rows =>
            {
                renderResultsCount++;
                capturedRowsForRenderer = rows;
            });

        var sut = new VersionCheckApplication(
            csvReaderMock.Object,
            repositoryLoaderMock.Object,
            versionCheckerMock.Object,
            rendererMock.Object);

        // Act
        var result = await sut.RunAsync(cts.Token);

        // Assert
        result.Should().Be(0);
        csvReadCount.Should().Be(1);
        renderHeaderCount.Should().Be(1);
        repositoryCheckCountCalls.Should().Be(1);

        loaderCallCount.Should().Be(1);
        capturedRepositories.Should().NotBeNull();
        capturedRepositories.Should().Equal(expectedRepositoryApi, expectedRepositoryGateway);
        var minVersions = capturedMinVersions ?? throw new InvalidOperationException("Expected min versions to be captured.");
        minVersions.Should().HaveCount(2);
        minVersions[expectedRepositoryApi].Should().Be(expectedApiMinVersion);
        minVersions[expectedRepositoryGateway].Should().Be(expectedGatewayMinVersion);
        capturedLoaderToken.Should().Be(cts.Token);

        checkerCallCount.Should().Be(1);
        capturedRowsForChecker.Should().NotBeNull();
        capturedRowsForChecker.Should().Equal(componentRows);
        capturedLookupsForChecker.Should().BeSameAs(tagLookups);

        renderResultsCount.Should().Be(1);
        capturedRowsForRenderer.Should().BeSameAs(expectedRows);
    }

    private static ComponentRow Component(string component, string repository, string version) =>
        new(
            new ComponentName(component),
            new RepositoryName(repository),
            new VersionLabel(version));
}
