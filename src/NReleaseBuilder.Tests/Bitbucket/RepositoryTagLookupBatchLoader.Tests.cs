using System.Text.Json;

using FluentAssertions;

using Moq;

using NReleaseBuilder.Abstractions.Bitbucket;
using NReleaseBuilder.Abstractions.Rendering;
using NReleaseBuilder.Bitbucket;
using NReleaseBuilder.Models;
using NReleaseBuilder.Models.Bitbucket;

using NuGet.Versioning;

namespace NReleaseBuilder.Tests.Bitbucket;

public class RepositoryTagLookupBatchLoaderTests
{
    [Fact(DisplayName = "RepositoryTagLookupBatchLoader can be created.")]
    [Trait("Category", "Unit")]
    public void RepositoryTagLookupBatchLoaderCanBeCreated()
    {
        // Arrange
        var bitbucketTagClient = new Mock<IBitbucketTagClient>(MockBehavior.Strict).Object;
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new RepositoryTagLookupBatchLoader(bitbucketTagClient, renderer));

        // Assert
        exception.Should().BeNull();
    }

    [Fact(DisplayName = "RepositoryTagLookupBatchLoader constructor throws when bitbucket client is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenBitbucketClientIsNull()
    {
        // Arrange
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new RepositoryTagLookupBatchLoader(null!, renderer));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "RepositoryTagLookupBatchLoader constructor throws when renderer is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenRendererIsNull()
    {
        // Arrange
        var bitbucketTagClient = new Mock<IBitbucketTagClient>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new RepositoryTagLookupBatchLoader(bitbucketTagClient, null!));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "RepositoryTagLookupBatchLoader LoadAsync throws when repositories are null.")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncThrowsWhenRepositoriesAreNull()
    {
        // Arrange
        var bitbucketTagClient = new Mock<IBitbucketTagClient>(MockBehavior.Strict).Object;
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;
        var sut = new RepositoryTagLookupBatchLoader(bitbucketTagClient, renderer);
        IReadOnlyDictionary<RepositoryName, NuGetVersion> minVersions =
            new Dictionary<RepositoryName, NuGetVersion>();

        // Act
        Func<Task> action = () => sut.LoadAsync(null!, minVersions, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("repositories");
    }

    [Fact(DisplayName = "RepositoryTagLookupBatchLoader LoadAsync throws when min versions are null.")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncThrowsWhenMinVersionsAreNull()
    {
        // Arrange
        var bitbucketTagClient = new Mock<IBitbucketTagClient>(MockBehavior.Strict).Object;
        var renderer = new Mock<IRenderer>(MockBehavior.Strict).Object;
        var sut = new RepositoryTagLookupBatchLoader(bitbucketTagClient, renderer);
        IReadOnlyList<RepositoryName> repositories = [new RepositoryName("repo-a")];

        // Act
        Func<Task> action = () => sut.LoadAsync(repositories, null!, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("minCurrentVersionsByRepository");
    }

    [Fact(DisplayName = "RepositoryTagLookupBatchLoader LoadAsync loads repositories in batches and merges results.")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncLoadsRepositoriesInBatchesAndMergesResults()
    {
        // Arrange
        var repositories = Enumerable.Range(1, 12)
            .Select(index => new RepositoryName($"repo-{index:D2}"))
            .ToArray();
        var batch1 = repositories.Take(10).ToArray();
        var batch2 = repositories.Skip(10).ToArray();
        IReadOnlyDictionary<RepositoryName, NuGetVersion> minVersions = repositories
            .ToDictionary(
                keySelector: repository => repository,
                elementSelector: _ => NuGetVersion.Parse("1.0.0"));
        var batch1Lookups = batch1.ToDictionary(
            keySelector: repository => repository,
            elementSelector: repository => RepositoryTagLookup.Success(repository, []));
        var batch2Lookups = batch2.ToDictionary(
            keySelector: repository => repository,
            elementSelector: repository => RepositoryTagLookup.Success(repository, []));
        using var cts = new CancellationTokenSource();
        var progressCallCount = 0;
        var runWithProgressCallCount = 0;
        var clientCallCount = 0;

        var bitbucketTagClientMock = new Mock<IBitbucketTagClient>(MockBehavior.Strict);
        bitbucketTagClientMock
            .Setup(x => x.FetchRepositoryTagLookupsAsync(
                It.Is<IReadOnlyList<RepositoryName>>(value => value.SequenceEqual(batch1)),
                It.Is<IReadOnlyDictionary<RepositoryName, NuGetVersion>>(value => ReferenceEquals(value, minVersions)),
                It.Is<BitbucketProgressCallbacks?>(value => value != null),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => clientCallCount++)
            .ReturnsAsync(batch1Lookups);
        bitbucketTagClientMock
            .Setup(x => x.FetchRepositoryTagLookupsAsync(
                It.Is<IReadOnlyList<RepositoryName>>(value => value.SequenceEqual(batch2)),
                It.Is<IReadOnlyDictionary<RepositoryName, NuGetVersion>>(value => ReferenceEquals(value, minVersions)),
                It.Is<BitbucketProgressCallbacks?>(value => value != null),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => clientCallCount++)
            .ReturnsAsync(batch2Lookups);

        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.PrintRepositoryBatchProgress(
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 2),
                It.Is<int>(value => value == 0),
                It.Is<int>(value => value == 10),
                It.Is<int>(value => value == 12)))
            .Callback(() => progressCallCount++);
        rendererMock
            .Setup(x => x.PrintRepositoryBatchProgress(
                It.Is<int>(value => value == 2),
                It.Is<int>(value => value == 2),
                It.Is<int>(value => value == 10),
                It.Is<int>(value => value == 2),
                It.Is<int>(value => value == 12)))
            .Callback(() => progressCallCount++);
        rendererMock
            .Setup(x => x.RunBitbucketLoadingWithProgressAsync(
                It.Is<IReadOnlyList<RepositoryName>>(value => value.SequenceEqual(batch1)),
                It.Is<Func<BitbucketProgressCallbacks, Task<Dictionary<RepositoryName, RepositoryTagLookup>>>>(value => value != null)))
            .Callback(() => runWithProgressCallCount++)
            .Returns<IReadOnlyList<RepositoryName>, Func<BitbucketProgressCallbacks, Task<Dictionary<RepositoryName, RepositoryTagLookup>>>>(
                (_, operation) => operation(new BitbucketProgressCallbacks()));
        rendererMock
            .Setup(x => x.RunBitbucketLoadingWithProgressAsync(
                It.Is<IReadOnlyList<RepositoryName>>(value => value.SequenceEqual(batch2)),
                It.Is<Func<BitbucketProgressCallbacks, Task<Dictionary<RepositoryName, RepositoryTagLookup>>>>(value => value != null)))
            .Callback(() => runWithProgressCallCount++)
            .Returns<IReadOnlyList<RepositoryName>, Func<BitbucketProgressCallbacks, Task<Dictionary<RepositoryName, RepositoryTagLookup>>>>(
                (_, operation) => operation(new BitbucketProgressCallbacks()));

        var sut = new RepositoryTagLookupBatchLoader(bitbucketTagClientMock.Object, rendererMock.Object);

        // Act
        var result = await sut.LoadAsync(repositories, minVersions, cts.Token);

        // Assert
        var lookups = result ?? throw new InvalidOperationException("Expected non-null lookup dictionary.");
        lookups.Should().HaveCount(12);
        lookups.Keys.Should().BeEquivalentTo(repositories);
        progressCallCount.Should().Be(2);
        runWithProgressCallCount.Should().Be(2);
        clientCallCount.Should().Be(2);
    }

    [Fact(DisplayName = "RepositoryTagLookupBatchLoader LoadAsync returns null and reports error on http failure.")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncReturnsNullAndReportsErrorOnHttpFailure()
    {
        // Arrange
        var exception = new HttpRequestException("bitbucket unavailable");
        var (repositories, minVersions) = CreateSingleRepositoryInput();
        using var cts = new CancellationTokenSource();
        var printErrorCallCount = 0;

        var bitbucketTagClient = new Mock<IBitbucketTagClient>(MockBehavior.Strict).Object;
        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.PrintRepositoryBatchProgress(
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 0),
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 1)));
        rendererMock
            .Setup(x => x.RunBitbucketLoadingWithProgressAsync(
                It.Is<IReadOnlyList<RepositoryName>>(value => value.SequenceEqual(repositories)),
                It.Is<Func<BitbucketProgressCallbacks, Task<Dictionary<RepositoryName, RepositoryTagLookup>>>>(value => value != null)))
            .ThrowsAsync(exception);
        rendererMock
            .Setup(x => x.PrintError(It.Is<ErrorMessage>(value =>
                value.Value.StartsWith("Failed to load tags from Bitbucket: ", StringComparison.Ordinal)
                && value.Value.Contains("bitbucket unavailable", StringComparison.Ordinal))))
            .Callback(() => printErrorCallCount++);

        var sut = new RepositoryTagLookupBatchLoader(bitbucketTagClient, rendererMock.Object);

        // Act
        var result = await sut.LoadAsync(repositories, minVersions, cts.Token);

        // Assert
        result.Should().BeNull();
        printErrorCallCount.Should().Be(1);
    }

    [Fact(DisplayName = "RepositoryTagLookupBatchLoader LoadAsync returns null and reports error on timeout.")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncReturnsNullAndReportsErrorOnTimeout()
    {
        // Arrange
        var exception = new TaskCanceledException("request timed out");
        var (repositories, minVersions) = CreateSingleRepositoryInput();
        using var cts = new CancellationTokenSource();
        var printErrorCallCount = 0;

        var bitbucketTagClient = new Mock<IBitbucketTagClient>(MockBehavior.Strict).Object;
        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.PrintRepositoryBatchProgress(
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 0),
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 1)));
        rendererMock
            .Setup(x => x.RunBitbucketLoadingWithProgressAsync(
                It.Is<IReadOnlyList<RepositoryName>>(value => value.SequenceEqual(repositories)),
                It.Is<Func<BitbucketProgressCallbacks, Task<Dictionary<RepositoryName, RepositoryTagLookup>>>>(value => value != null)))
            .ThrowsAsync(exception);
        rendererMock
            .Setup(x => x.PrintError(It.Is<ErrorMessage>(value =>
                value.Value.StartsWith("Failed to load tags from Bitbucket: ", StringComparison.Ordinal)
                && value.Value.Contains("request timed out", StringComparison.Ordinal))))
            .Callback(() => printErrorCallCount++);

        var sut = new RepositoryTagLookupBatchLoader(bitbucketTagClient, rendererMock.Object);

        // Act
        var result = await sut.LoadAsync(repositories, minVersions, cts.Token);

        // Assert
        result.Should().BeNull();
        printErrorCallCount.Should().Be(1);
    }

    [Fact(DisplayName = "RepositoryTagLookupBatchLoader LoadAsync returns null and reports error on json failure.")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncReturnsNullAndReportsErrorOnJsonFailure()
    {
        // Arrange
        var exception = new JsonException("invalid payload");
        var (repositories, minVersions) = CreateSingleRepositoryInput();
        using var cts = new CancellationTokenSource();
        var printErrorCallCount = 0;

        var bitbucketTagClient = new Mock<IBitbucketTagClient>(MockBehavior.Strict).Object;
        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.PrintRepositoryBatchProgress(
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 0),
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 1)));
        rendererMock
            .Setup(x => x.RunBitbucketLoadingWithProgressAsync(
                It.Is<IReadOnlyList<RepositoryName>>(value => value.SequenceEqual(repositories)),
                It.Is<Func<BitbucketProgressCallbacks, Task<Dictionary<RepositoryName, RepositoryTagLookup>>>>(value => value != null)))
            .ThrowsAsync(exception);
        rendererMock
            .Setup(x => x.PrintError(It.Is<ErrorMessage>(value =>
                value.Value.StartsWith("Failed to load tags from Bitbucket: ", StringComparison.Ordinal)
                && value.Value.Contains("invalid payload", StringComparison.Ordinal))))
            .Callback(() => printErrorCallCount++);

        var sut = new RepositoryTagLookupBatchLoader(bitbucketTagClient, rendererMock.Object);

        // Act
        var result = await sut.LoadAsync(repositories, minVersions, cts.Token);

        // Assert
        result.Should().BeNull();
        printErrorCallCount.Should().Be(1);
    }

    [Fact(DisplayName = "RepositoryTagLookupBatchLoader LoadAsync returns null and reports error on invalid operation.")]
    [Trait("Category", "Unit")]
    public async Task LoadAsyncReturnsNullAndReportsErrorOnInvalidOperation()
    {
        // Arrange
        var exception = new InvalidOperationException("unexpected state");
        var (repositories, minVersions) = CreateSingleRepositoryInput();
        using var cts = new CancellationTokenSource();
        var printErrorCallCount = 0;

        var bitbucketTagClient = new Mock<IBitbucketTagClient>(MockBehavior.Strict).Object;
        var rendererMock = new Mock<IRenderer>(MockBehavior.Strict);
        rendererMock
            .Setup(x => x.PrintRepositoryBatchProgress(
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 0),
                It.Is<int>(value => value == 1),
                It.Is<int>(value => value == 1)));
        rendererMock
            .Setup(x => x.RunBitbucketLoadingWithProgressAsync(
                It.Is<IReadOnlyList<RepositoryName>>(value => value.SequenceEqual(repositories)),
                It.Is<Func<BitbucketProgressCallbacks, Task<Dictionary<RepositoryName, RepositoryTagLookup>>>>(value => value != null)))
            .ThrowsAsync(exception);
        rendererMock
            .Setup(x => x.PrintError(It.Is<ErrorMessage>(value =>
                value.Value.StartsWith("Failed to load tags from Bitbucket: ", StringComparison.Ordinal)
                && value.Value.Contains("unexpected state", StringComparison.Ordinal))))
            .Callback(() => printErrorCallCount++);

        var sut = new RepositoryTagLookupBatchLoader(bitbucketTagClient, rendererMock.Object);

        // Act
        var result = await sut.LoadAsync(repositories, minVersions, cts.Token);

        // Assert
        result.Should().BeNull();
        printErrorCallCount.Should().Be(1);
    }

    private static (IReadOnlyList<RepositoryName> Repositories, IReadOnlyDictionary<RepositoryName, NuGetVersion> MinVersions)
        CreateSingleRepositoryInput()
    {
        var repository = new RepositoryName("repo-a");
        IReadOnlyList<RepositoryName> repositories = [repository];
        IReadOnlyDictionary<RepositoryName, NuGetVersion> minVersions =
            new Dictionary<RepositoryName, NuGetVersion>
            {
                [repository] = NuGetVersion.Parse("1.0.0"),
            };

        return (repositories, minVersions);
    }
}
