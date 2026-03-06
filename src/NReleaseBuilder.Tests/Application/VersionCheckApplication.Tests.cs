using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using NuGet.Versioning;

using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Abstractions.Csv;
using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Application;
using NReleaseBuilder.Bitbucket;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Components;
using NReleaseBuilder.Models.Rendering;

namespace NReleaseBuilder.Tests.Application;

public class VersionCheckApplicationTests
{
    [Fact(DisplayName = "VersionCheckApplication can be created.")]
    [Trait("Category", "Unit")]
    public void VersionCheckApplicationCanBeCreated()
    {
        // Arrange
        var csvReader = new Mock<ICsvComponentReader>(MockBehavior.Strict).Object;
        var componentsVersionBuilder = new Mock<IComponentsVersionBuilder>(MockBehavior.Strict).Object;
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new VersionCheckApplication(
            csvReader,
            CreateRepositoryNameNormalizer(),
            componentsVersionBuilder,
            renderer,
            CreateOptions()));

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "VersionCheckApplication cant be created with null csv reader.")]
    [Trait("Category", "Unit")]
    public void VersionCheckApplicationCantBeCreatedWithNullCsvReader()
    {
        // Arrange
        var componentsVersionBuilder = new Mock<IComponentsVersionBuilder>(MockBehavior.Strict).Object;
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new VersionCheckApplication(
            null!,
            CreateRepositoryNameNormalizer(),
            componentsVersionBuilder,
            renderer,
            CreateOptions()));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "VersionCheckApplication cant be created with null components version builder.")]
    [Trait("Category", "Unit")]
    public void VersionCheckApplicationCantBeCreatedWithNullComponentsVersionBuilder()
    {
        // Arrange
        var csvReader = new Mock<ICsvComponentReader>(MockBehavior.Strict).Object;
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new VersionCheckApplication(
            csvReader,
            CreateRepositoryNameNormalizer(),
            null!,
            renderer,
            CreateOptions()));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "VersionCheckApplication cant be created with null renderer.")]
    [Trait("Category", "Unit")]
    public void VersionCheckApplicationCantBeCreatedWithNullRenderer()
    {
        // Arrange
        var csvReader = new Mock<ICsvComponentReader>(MockBehavior.Strict).Object;
        var componentsVersionBuilder = new Mock<IComponentsVersionBuilder>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new VersionCheckApplication(
            csvReader,
            CreateRepositoryNameNormalizer(),
            componentsVersionBuilder,
            null!,
            CreateOptions()));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "VersionCheckApplication cant be created with null options.")]
    [Trait("Category", "Unit")]
    public void VersionCheckApplicationCantBeCreatedWithNullOptions()
    {
        // Arrange
        var csvReader = new Mock<ICsvComponentReader>(MockBehavior.Strict).Object;
        var componentsVersionBuilder = new Mock<IComponentsVersionBuilder>(MockBehavior.Strict).Object;
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new VersionCheckApplication(
            csvReader,
            CreateRepositoryNameNormalizer(),
            componentsVersionBuilder,
            renderer,
            null!));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "RunAsync returns 0 when csv read fails.")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncReturnsZeroWhenCsvReadFails()
    {
        // Arrange
        var csvReadCount = 0;
        var renderHeaderCount = 0;
        var csvReaderMock = new Mock<ICsvComponentReader>(MockBehavior.Strict);
        csvReaderMock
            .Setup(x => x.Read())
            .Callback(() => csvReadCount++)
            .Returns((IReadOnlyList<ComponentRow>?)null);

        var componentsVersionBuilderMock = new Mock<IComponentsVersionBuilder>(MockBehavior.Strict);
        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.SetupContext(It.IsAny<ReportRunDefinition>()));
        rendererMock
            .Setup(x => x.ResetContext());
        rendererMock
            .Setup(x => x.RenderHeader())
            .Callback(() => renderHeaderCount++);
        rendererMock
            .Setup(x => x.PrintRepositoryCheckCount(It.Is<int>(count => count == 2)));
        rendererMock
            .Setup(x => x.PrintRepositoryCheckCount(It.Is<int>(count => count == 2)));

        var sut = new VersionCheckApplication(
            csvReaderMock.Object,
            CreateRepositoryNameNormalizer(),
            componentsVersionBuilderMock.Object,
            rendererMock.Object,
            CreateOptions());

        // Act
        var result = await sut.RunAsync(CancellationToken.None);

        // Assert
        result.Should().Be(0);
        csvReadCount.Should().Be(1);
        renderHeaderCount.Should().Be(1);
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

        var componentsVersionBuilderMock = new Mock<IComponentsVersionBuilder>(MockBehavior.Strict);
        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.SetupContext(It.IsAny<ReportRunDefinition>()));
        rendererMock
            .Setup(x => x.ResetContext());
        rendererMock
            .Setup(x => x.RenderHeader())
            .Callback(() => renderHeaderCount++);
        rendererMock
            .Setup(x => x.PrintNoRows())
            .Callback(() => printNoRowsCount++);

        var sut = new VersionCheckApplication(
            csvReaderMock.Object,
            CreateRepositoryNameNormalizer(),
            componentsVersionBuilderMock.Object,
            rendererMock.Object,
            CreateOptions());

        // Act
        var result = await sut.RunAsync(CancellationToken.None);

        // Assert
        result.Should().Be(0);
        csvReadCount.Should().Be(1);
        renderHeaderCount.Should().Be(1);
        printNoRowsCount.Should().Be(1);
    }

    [Fact(DisplayName = "RunAsync returns 0 when components version build fails.")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncReturnsZeroWhenComponentsVersionBuildFails()
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
        var builderCallCount = 0;
        IReadOnlyList<ComponentRow>? capturedRows = null;
        RepositoryVersionContext? capturedContext = null;
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

        var componentsVersionBuilderMock = new Mock<IComponentsVersionBuilder>(MockBehavior.Strict);
        componentsVersionBuilderMock
            .Setup(x => x.BuildAsync(
                It.Is<IReadOnlyList<ComponentRow>>(rows =>
                    rows.Count == componentRows.Count),
                It.IsAny<RepositoryVersionContext>(),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .Callback<IReadOnlyList<ComponentRow>, RepositoryVersionContext, CancellationToken>((rows, context, token) =>
            {
                builderCallCount++;
                capturedRows = rows;
                capturedContext = context;
                capturedToken = token;
            })
            .ReturnsAsync((IReadOnlyList<ComponentCheckRow>?)null);

        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.SetupContext(It.IsAny<ReportRunDefinition>()));
        rendererMock
            .Setup(x => x.ResetContext());
        rendererMock
            .Setup(x => x.RenderHeader())
            .Callback(() => renderHeaderCount++);
        rendererMock
            .Setup(x => x.PrintRepositoryCheckCount(It.Is<int>(count => count == 2)));

        var sut = new VersionCheckApplication(
            csvReaderMock.Object,
            CreateRepositoryNameNormalizer(),
            componentsVersionBuilderMock.Object,
            rendererMock.Object,
            CreateOptions());

        // Act
        var result = await sut.RunAsync(cts.Token);

        // Assert
        result.Should().Be(0);
        csvReadCount.Should().Be(1);
        renderHeaderCount.Should().Be(1);
        builderCallCount.Should().Be(1);

        capturedRows.Should().NotBeNull();
        capturedRows.Should().Equal(componentRows);

        var repositoryContext = capturedContext ?? throw new InvalidOperationException("Expected repository context to be captured.");
        repositoryContext.Repositories.Should().Equal(expectedRepositoryApi, expectedRepositoryGateway);
        repositoryContext.MinCurrentVersionsByRepository.Should().HaveCount(2);
        repositoryContext.MinCurrentVersionsByRepository[expectedRepositoryApi].Should().Be(expectedApiMinVersion);
        repositoryContext.MinCurrentVersionsByRepository[expectedRepositoryGateway].Should().Be(expectedGatewayMinVersion);
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
        var builderCallCount = 0;
        IReadOnlyList<ComponentRow>? capturedRowsForBuilder = null;
        RepositoryVersionContext? capturedContextForBuilder = null;
        var capturedBuilderToken = CancellationToken.None;
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

        var componentsVersionBuilderMock = new Mock<IComponentsVersionBuilder>(MockBehavior.Strict);
        componentsVersionBuilderMock
            .Setup(x => x.BuildAsync(
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
                It.Is<RepositoryVersionContext>(context =>
                    context.Repositories.Count == 2
                    && context.Repositories[0] == expectedRepositoryApi
                    && context.Repositories[1] == expectedRepositoryGateway
                    && context.MinCurrentVersionsByRepository.Count == 2
                    && context.MinCurrentVersionsByRepository.ContainsKey(expectedRepositoryApi)
                    && context.MinCurrentVersionsByRepository[expectedRepositoryApi] == expectedApiMinVersion
                    && context.MinCurrentVersionsByRepository.ContainsKey(expectedRepositoryGateway)
                    && context.MinCurrentVersionsByRepository[expectedRepositoryGateway] == expectedGatewayMinVersion),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .Callback<IReadOnlyList<ComponentRow>, RepositoryVersionContext, CancellationToken>((rows, context, token) =>
            {
                builderCallCount++;
                capturedRowsForBuilder = rows;
                capturedContextForBuilder = context;
                capturedBuilderToken = token;
            })
            .ReturnsAsync(expectedRows);

        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.SetupContext(It.IsAny<ReportRunDefinition>()));
        rendererMock
            .Setup(x => x.ResetContext());
        rendererMock
            .Setup(x => x.RenderHeader())
            .Callback(() => renderHeaderCount++);
        rendererMock
            .Setup(x => x.PrintRepositoryCheckCount(It.Is<int>(count => count == 2)));
        rendererMock
            .Setup(x => x.RenderResults(It.Is<IReadOnlyList<ComponentCheckRow>>(rows => ReferenceEquals(rows, expectedRows))))
            .Callback<IReadOnlyList<ComponentCheckRow>>(rows =>
            {
                renderResultsCount++;
                capturedRowsForRenderer = rows;
            });

        var sut = new VersionCheckApplication(
            csvReaderMock.Object,
            CreateRepositoryNameNormalizer(),
            componentsVersionBuilderMock.Object,
            rendererMock.Object,
            CreateOptions());

        // Act
        var result = await sut.RunAsync(cts.Token);

        // Assert
        result.Should().Be(0);
        csvReadCount.Should().Be(1);
        renderHeaderCount.Should().Be(1);

        builderCallCount.Should().Be(1);
        capturedRowsForBuilder.Should().NotBeNull();
        capturedRowsForBuilder.Should().Equal(componentRows);
        capturedContextForBuilder.Should().NotBeNull();
        capturedBuilderToken.Should().Be(cts.Token);

        renderResultsCount.Should().Be(1);
        capturedRowsForRenderer.Should().BeSameAs(expectedRows);
    }

    [Fact(DisplayName = "RunAsync applies repository overrides before building repository context.")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncAppliesRepositoryOverridesBeforeBuildingRepositoryContext()
    {
        // Arrange
        var componentRows = new List<ComponentRow>
        {
            Component("api", "repo-api", "1.0.0"),
            Component("gateway", "repo-gateway", "0.9.0"),
        };

        var normalizedRepository = new RepositoryName("repo-shared");
        var expectedMinVersion = NuGetVersion.Parse("0.9.0");
        IReadOnlyList<ComponentCheckRow> expectedRows = [];
        using var cts = new CancellationTokenSource();
        var repositoryNameOverrides = new Dictionary<string, string>
        {
            ["repo-api"] = "repo-shared",
            ["repo-gateway"] = "repo-shared",
        };

        var csvReaderMock = new Mock<ICsvComponentReader>(MockBehavior.Strict);
        csvReaderMock
            .Setup(x => x.Read())
            .Returns(componentRows);

        var componentsVersionBuilderMock = new Mock<IComponentsVersionBuilder>(MockBehavior.Strict);
        componentsVersionBuilderMock
            .Setup(x => x.BuildAsync(
                It.Is<IReadOnlyList<ComponentRow>>(rows =>
                    rows.Count == 2
                    && rows[0].Repository == normalizedRepository
                    && rows[1].Repository == normalizedRepository
                    && rows[0].Component == componentRows[0].Component
                    && rows[1].Component == componentRows[1].Component),
                It.Is<RepositoryVersionContext>(context =>
                    context.Repositories.Count == 1
                    && context.Repositories[0] == normalizedRepository
                    && context.MinCurrentVersionsByRepository.Count == 1
                    && context.MinCurrentVersionsByRepository.ContainsKey(normalizedRepository)
                    && context.MinCurrentVersionsByRepository[normalizedRepository] == expectedMinVersion),
                It.Is<CancellationToken>(token => token == cts.Token)))
            .ReturnsAsync(expectedRows);

        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.SetupContext(It.IsAny<ReportRunDefinition>()));
        rendererMock
            .Setup(x => x.ResetContext());
        rendererMock
            .Setup(x => x.RenderHeader());
        rendererMock
            .Setup(x => x.PrintRepositoryCheckCount(It.Is<int>(count => count == 1)));
        rendererMock
            .Setup(x => x.RenderResults(It.Is<IReadOnlyList<ComponentCheckRow>>(rows => ReferenceEquals(rows, expectedRows))));

        var sut = new VersionCheckApplication(
            csvReaderMock.Object,
            CreateRepositoryNameNormalizer(repositoryNameOverrides),
            componentsVersionBuilderMock.Object,
            rendererMock.Object,
            CreateOptions(repositoryNameOverrides));

        // Act
        var result = await sut.RunAsync(cts.Token);

        // Assert
        result.Should().Be(0);
    }

    private static ComponentRow Component(string component, string repository, string version) =>
        new(
            new ComponentName(component),
            new RepositoryName(repository),
            new VersionLabel(version));

    private static RepositoryNameNormalizer CreateRepositoryNameNormalizer(
        IReadOnlyDictionary<string, string>? repositoryNameOverrides = null) =>
        new(CreateOptions(repositoryNameOverrides));

    private static IOptions<AppSettings> CreateOptions(
        IReadOnlyDictionary<string, string>? repositoryNameOverrides = null)
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
                RepositoryNameOverrides = repositoryNameOverrides ?? new Dictionary<string, string>(),
            },
            Jira = new JiraOptions
            {
                BaseUrl = new Uri("https://jira.example.test/"),
            },
        };

        return Options.Create(settings);
    }
}
