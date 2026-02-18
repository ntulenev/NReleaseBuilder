using System.Net;
using System.Text;

using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using NReleaseBuilder.Abstractions.Transport;
using NReleaseBuilder.Bitbucket.Internal;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Models.Bitbucket;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Tests.Bitbucket.Internal;

public class BitbucketIntegrationCoreTests
{
    [Fact(DisplayName = "BitbucketIntegrationCore constructor throws when http client factory is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenHttpClientFactoryIsNull()
    {
        // Arrange
        var options = CreateOptions(pageLen: 5, retryCount: 2);
        var retryExecutor = new Mock<IHttpRetryExecutor>(MockBehavior.Strict).Object;
        var serializer = new Mock<IResponseSerializer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new BitbucketIntegrationCore(null!, options, retryExecutor, serializer));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "BitbucketIntegrationCore constructor throws when options are null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenOptionsAreNull()
    {
        // Arrange
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict).Object;
        var retryExecutor = new Mock<IHttpRetryExecutor>(MockBehavior.Strict).Object;
        var serializer = new Mock<IResponseSerializer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new BitbucketIntegrationCore(factory, null!, retryExecutor, serializer));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "BitbucketIntegrationCore constructor throws when retry executor is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenRetryExecutorIsNull()
    {
        // Arrange
        var options = CreateOptions(pageLen: 5, retryCount: 2);
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict).Object;
        var serializer = new Mock<IResponseSerializer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new BitbucketIntegrationCore(factory, options, null!, serializer));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "BitbucketIntegrationCore constructor throws when serializer is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenSerializerIsNull()
    {
        // Arrange
        var options = CreateOptions(pageLen: 5, retryCount: 2);
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict).Object;
        var retryExecutor = new Mock<IHttpRetryExecutor>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new BitbucketIntegrationCore(factory, options, retryExecutor, null!));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "BitbucketIntegrationCore LoadRepositoryTagReferencesAsync returns repo not found for 404.")]
    [Trait("Category", "Unit")]
    public async Task LoadRepositoryTagReferencesAsyncReturnsRepoNotFoundFor404()
    {
        // Arrange
        var options = CreateOptions(pageLen: 5, retryCount: 2);
        var repository = new RepositoryName("service.api");
        var expectedRelativeUrl = "repositories/workspace/service.api/refs/tags?pagelen=5";
        var notFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
        using var httpClient = new HttpClient();
        using var cts = new CancellationTokenSource();

        var factoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.BITBUCKET)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == expectedRelativeUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(notFoundResponse);

        var serializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);
        var sut = new BitbucketIntegrationCore(factoryMock.Object, options, retryExecutorMock.Object, serializerMock.Object);

        // Act
        var result = await sut.LoadRepositoryTagReferencesAsync(repository, cts.Token);

        // Assert
        result.IsRepositoryMissing.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Tags.Should().BeEmpty();
    }

    [Fact(DisplayName = "BitbucketIntegrationCore LoadRepositoryTagReferencesAsync returns api error details for non success status.")]
    [Trait("Category", "Unit")]
    public async Task LoadRepositoryTagReferencesAsyncReturnsApiErrorDetailsForNonSuccessStatus()
    {
        // Arrange
        var options = CreateOptions(pageLen: 5, retryCount: 2);
        var repository = new RepositoryName("service.api");
        var expectedRelativeUrl = "repositories/workspace/service.api/refs/tags?pagelen=5";
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            ReasonPhrase = "Bad Gateway",
            Content = new StringContent("first line\r\nsecond line", Encoding.UTF8, "text/plain"),
        };
        using var httpClient = new HttpClient();
        using var cts = new CancellationTokenSource();

        var factoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.BITBUCKET)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == expectedRelativeUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(response);

        var serializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);
        var sut = new BitbucketIntegrationCore(factoryMock.Object, options, retryExecutorMock.Object, serializerMock.Object);

        // Act
        var result = await sut.LoadRepositoryTagReferencesAsync(repository, cts.Token);

        // Assert
        result.IsRepositoryMissing.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Should().StartWith("502 Bad Gateway: ");
        result.Error.Should().Contain("first line second line");
        result.Tags.Should().BeEmpty();
    }

    [Fact(DisplayName = "BitbucketIntegrationCore LoadRepositoryTagReferencesAsync aggregates pages and de-duplicates tags.")]
    [Trait("Category", "Unit")]
    public async Task LoadRepositoryTagReferencesAsyncAggregatesPagesAndDeduplicatesTags()
    {
        // Arrange
        var options = CreateOptions(pageLen: 5, retryCount: 2);
        var repository = new RepositoryName("service.api");
        var firstUrl = "repositories/workspace/service.api/refs/tags?pagelen=5";
        const string secondUrl = "https://bitbucket.example.test/tags?page=2";
        var firstResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var secondResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var page1 = new TagPageDto
        {
            Values =
            [
                new TagDto
                {
                    Name = "v1.0.0",
                    Target = new TagTargetDto { Hash = "abc" },
                },
                new TagDto
                {
                    Name = "V1.0.0",
                    Target = new TagTargetDto { Hash = "def" },
                },
            ],
            Next = secondUrl,
        };
        var page2 = new TagPageDto
        {
            Values =
            [
                new TagDto
                {
                    Name = "v1.1.0",
                    Target = new TagTargetDto { Hash = null },
                },
            ],
            Next = null,
        };
        using var httpClient = new HttpClient();
        using var cts = new CancellationTokenSource();
        var retryCallCount = 0;
        var serializeCallCount = 0;

        var factoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.BITBUCKET)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == firstUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => retryCallCount++)
            .ReturnsAsync(firstResponse);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == secondUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => retryCallCount++)
            .ReturnsAsync(secondResponse);

        var serializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);
        serializerMock
            .Setup(x => x.SerializeAsync<TagPageDto>(
                It.Is<HttpResponseMessage>(value => ReferenceEquals(value, firstResponse)),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => serializeCallCount++)
            .ReturnsAsync(page1);
        serializerMock
            .Setup(x => x.SerializeAsync<TagPageDto>(
                It.Is<HttpResponseMessage>(value => ReferenceEquals(value, secondResponse)),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => serializeCallCount++)
            .ReturnsAsync(page2);

        var sut = new BitbucketIntegrationCore(factoryMock.Object, options, retryExecutorMock.Object, serializerMock.Object);

        // Act
        var result = await sut.LoadRepositoryTagReferencesAsync(repository, cts.Token);

        // Assert
        result.IsRepositoryMissing.Should().BeFalse();
        result.Error.Should().BeNull();
        result.Tags.Should().HaveCount(2);
        result.Tags[0].Name.Value.Should().Be("v1.0.0");
        var firstHash = result.Tags[0].CommitHash
            ?? throw new InvalidOperationException("Expected first tag hash.");
        firstHash.Value.Should().Be("abc");
        result.Tags[1].Name.Value.Should().Be("v1.1.0");
        result.Tags[1].CommitHash.Should().BeNull();
        retryCallCount.Should().Be(2);
        serializeCallCount.Should().Be(2);
    }

    [Fact(DisplayName = "BitbucketIntegrationCore TryGetCommitMessageAsync returns empty commit info for non success status.")]
    [Trait("Category", "Unit")]
    public async Task TryGetCommitMessageAsyncReturnsEmptyCommitInfoForNonSuccessStatus()
    {
        // Arrange
        var options = CreateOptions(pageLen: 5, retryCount: 2);
        var repository = new RepositoryName("service.api");
        var commitHash = new CommitHash("abc123");
        var expectedCommitUrl = "repositories/workspace/service.api/commit/abc123";
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        using var httpClient = new HttpClient();
        using var cts = new CancellationTokenSource();

        var factoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.BITBUCKET)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == expectedCommitUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(response);

        var serializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);
        var sut = new BitbucketIntegrationCore(factoryMock.Object, options, retryExecutorMock.Object, serializerMock.Object);

        // Act
        var result = await sut.TryGetCommitMessageAsync(repository, commitHash, cts.Token);

        // Assert
        result.Message.Should().BeNull();
    }

    [Fact(DisplayName = "BitbucketIntegrationCore TryGetCommitMessageAsync returns empty commit info when payload is null.")]
    [Trait("Category", "Unit")]
    public async Task TryGetCommitMessageAsyncReturnsEmptyCommitInfoWhenPayloadIsNull()
    {
        // Arrange
        var options = CreateOptions(pageLen: 5, retryCount: 2);
        var repository = new RepositoryName("service.api");
        var commitHash = new CommitHash("abc123");
        var expectedCommitUrl = "repositories/workspace/service.api/commit/abc123";
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        using var httpClient = new HttpClient();
        using var cts = new CancellationTokenSource();

        var factoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.BITBUCKET)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == expectedCommitUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(response);

        var serializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);
        serializerMock
            .Setup(x => x.SerializeAsync<CommitDto>(
                It.Is<HttpResponseMessage>(value => ReferenceEquals(value, response)),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync((CommitDto?)null);

        var sut = new BitbucketIntegrationCore(factoryMock.Object, options, retryExecutorMock.Object, serializerMock.Object);

        // Act
        var result = await sut.TryGetCommitMessageAsync(repository, commitHash, cts.Token);

        // Assert
        result.Message.Should().BeNull();
    }

    [Fact(DisplayName = "BitbucketIntegrationCore TryGetCommitMessageAsync maps commit dto message.")]
    [Trait("Category", "Unit")]
    public async Task TryGetCommitMessageAsyncMapsCommitDtoMessage()
    {
        // Arrange
        var options = CreateOptions(pageLen: 5, retryCount: 2);
        var repository = new RepositoryName("service.api");
        var commitHash = new CommitHash("abc123");
        var expectedCommitUrl = "repositories/workspace/service.api/commit/abc123";
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        var commitDto = new CommitDto
        {
            Message = "  feat: add background jobs  ",
        };
        using var httpClient = new HttpClient();
        using var cts = new CancellationTokenSource();

        var factoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.BITBUCKET)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == expectedCommitUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(response);

        var serializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);
        serializerMock
            .Setup(x => x.SerializeAsync<CommitDto>(
                It.Is<HttpResponseMessage>(value => ReferenceEquals(value, response)),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(commitDto);

        var sut = new BitbucketIntegrationCore(factoryMock.Object, options, retryExecutorMock.Object, serializerMock.Object);

        // Act
        var result = await sut.TryGetCommitMessageAsync(repository, commitHash, cts.Token);

        // Assert
        result.Message.Should().Be("feat: add background jobs");
    }

    private static IOptions<AppSettings> CreateOptions(int pageLen, int retryCount)
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
                PageLen = pageLen,
                RetryCount = retryCount,
            },
            Jira = new JiraOptions
            {
                BaseUrl = new Uri("https://jira.example.test/"),
            },
        };

        return Options.Create(settings);
    }
}
