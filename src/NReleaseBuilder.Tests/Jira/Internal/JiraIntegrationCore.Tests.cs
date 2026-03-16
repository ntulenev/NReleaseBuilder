using System.Net;

using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using NReleaseBuilder.Abstractions.Transport;
using NReleaseBuilder.Configuration;
using NReleaseBuilder.Jira.Internal;
using NReleaseBuilder.Jira.Internal.Models;
using NReleaseBuilder.Models.Jira;
using NReleaseBuilder.Transport.Models;

namespace NReleaseBuilder.Tests.Jira.Internal;

public class JiraIntegrationCoreTests
{
    [Fact(DisplayName = "JiraIntegrationCore constructor throws when http client factory is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenHttpClientFactoryIsNull()
    {
        // Arrange
        var options = CreateOptions(retryCount: 2);
        var retryExecutor = new Mock<IHttpRetryExecutor>(MockBehavior.Strict).Object;
        var responseSerializer = new Mock<IResponseSerializer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new JiraIntegrationCore(null!, options, retryExecutor, responseSerializer));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "JiraIntegrationCore constructor throws when options are null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenOptionsAreNull()
    {
        // Arrange
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict).Object;
        var retryExecutor = new Mock<IHttpRetryExecutor>(MockBehavior.Strict).Object;
        var responseSerializer = new Mock<IResponseSerializer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new JiraIntegrationCore(httpClientFactory, null!, retryExecutor, responseSerializer));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "JiraIntegrationCore constructor throws when retry executor is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenRetryExecutorIsNull()
    {
        // Arrange
        var options = CreateOptions(retryCount: 2);
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict).Object;
        var responseSerializer = new Mock<IResponseSerializer>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new JiraIntegrationCore(httpClientFactory, options, null!, responseSerializer));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "JiraIntegrationCore constructor throws when response serializer is null.")]
    [Trait("Category", "Unit")]
    public void ConstructorThrowsWhenResponseSerializerIsNull()
    {
        // Arrange
        var options = CreateOptions(retryCount: 2);
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict).Object;
        var retryExecutor = new Mock<IHttpRetryExecutor>(MockBehavior.Strict).Object;

        // Act
        var exception = Record.Exception(() => new JiraIntegrationCore(httpClientFactory, options, retryExecutor, null!));

        // Assert
        exception.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
    }

    [Fact(DisplayName = "JiraIntegrationCore TryGetJiraTaskInfoAsync returns http error for non success non 404 response.")]
    [Trait("Category", "Unit")]
    public async Task TryGetJiraTaskInfoAsyncReturnsHttpErrorForNonSuccessNon404Response()
    {
        // Arrange
        var options = CreateOptions(retryCount: 2);
        var jiraTask = new JiraTaskReference("PROJ-1");
        var issueUrl = BuildIssueUrl(jiraTask);
        using var httpClient = new HttpClient();
        var issueResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        using var cts = new CancellationTokenSource();

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.JIRA)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == issueUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(issueResponse);

        var responseSerializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);

        var sut = new JiraIntegrationCore(
            httpClientFactoryMock.Object,
            options,
            retryExecutorMock.Object,
            responseSerializerMock.Object);

        // Act
        var result = await sut.TryGetJiraTaskInfoAsync(jiraTask, cts.Token);

        // Assert
        result.Should().Be(JiraTaskInfo.HttpError(HttpStatusCode.InternalServerError));
    }

    [Fact(DisplayName = "JiraIntegrationCore TryGetJiraTaskInfoAsync resolves not found status from api3 search.")]
    [Trait("Category", "Unit")]
    public async Task TryGetJiraTaskInfoAsyncResolvesNotFoundStatusFromApi3Search()
    {
        // Arrange
        var options = CreateOptions(retryCount: 2);
        var jiraTask = new JiraTaskReference("PROJ-1");
        var issueUrl = BuildIssueUrl(jiraTask);
        var api3SearchUrl = BuildApi3SearchUrl(jiraTask);
        using var httpClient = new HttpClient();
        var issueNotFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
        var api3SearchResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var searchResponseDto = new JiraSearchResponseDto
        {
            Issues =
            [
                new JiraSearchIssueDto
                {
                    Fields = new JiraIssueFieldsDto
                    {
                        Status = new JiraStatusDto
                        {
                            Name = "In Progress",
                        },
                    },
                },
            ],
        };
        using var cts = new CancellationTokenSource();
        var retryCallCount = 0;
        var serializerCallCount = 0;

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.JIRA)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == issueUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => retryCallCount++)
            .ReturnsAsync(issueNotFoundResponse);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == api3SearchUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => retryCallCount++)
            .ReturnsAsync(api3SearchResponse);

        var responseSerializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);
        responseSerializerMock
            .Setup(x => x.SerializeAsync<JiraSearchResponseDto>(
                It.Is<HttpResponseMessage>(value => ReferenceEquals(value, api3SearchResponse)),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => serializerCallCount++)
            .ReturnsAsync(searchResponseDto);

        var sut = new JiraIntegrationCore(
            httpClientFactoryMock.Object,
            options,
            retryExecutorMock.Object,
            responseSerializerMock.Object);

        // Act
        var result = await sut.TryGetJiraTaskInfoAsync(jiraTask, cts.Token);

        // Assert
        result.Status.Should().Be("In Progress");
        result.Title.Should().Be("N/A");
        result.RequiredActionsDetails.Should().BeNull();
        result.BreakingChangesDetails.Should().BeNull();
        retryCallCount.Should().Be(2);
        serializerCallCount.Should().Be(1);
    }

    [Fact(DisplayName = "JiraIntegrationCore TryGetJiraTaskInfoAsync falls back to api2 search when api3 returns 404.")]
    [Trait("Category", "Unit")]
    public async Task TryGetJiraTaskInfoAsyncFallsBackToApi2SearchWhenApi3Returns404()
    {
        // Arrange
        var options = CreateOptions(retryCount: 2);
        var jiraTask = new JiraTaskReference("PROJ-1");
        var issueUrl = BuildIssueUrl(jiraTask);
        var api3SearchUrl = BuildApi3SearchUrl(jiraTask);
        var api2SearchUrl = BuildApi2SearchUrl(jiraTask);
        using var httpClient = new HttpClient();
        var issueNotFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
        var api3NotFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
        var api2SearchResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var searchResponseDto = new JiraSearchResponseDto
        {
            Issues =
            [
                new JiraSearchIssueDto
                {
                    Fields = new JiraIssueFieldsDto
                    {
                        Status = new JiraStatusDto
                        {
                            Name = "Done",
                        },
                    },
                },
            ],
        };
        using var cts = new CancellationTokenSource();
        var retryCallCount = 0;
        var serializerCallCount = 0;

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.JIRA)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == issueUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => retryCallCount++)
            .ReturnsAsync(issueNotFoundResponse);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == api3SearchUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => retryCallCount++)
            .ReturnsAsync(api3NotFoundResponse);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == api2SearchUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => retryCallCount++)
            .ReturnsAsync(api2SearchResponse);

        var responseSerializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);
        responseSerializerMock
            .Setup(x => x.SerializeAsync<JiraSearchResponseDto>(
                It.Is<HttpResponseMessage>(value => ReferenceEquals(value, api2SearchResponse)),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => serializerCallCount++)
            .ReturnsAsync(searchResponseDto);

        var sut = new JiraIntegrationCore(
            httpClientFactoryMock.Object,
            options,
            retryExecutorMock.Object,
            responseSerializerMock.Object);

        // Act
        var result = await sut.TryGetJiraTaskInfoAsync(jiraTask, cts.Token);

        // Assert
        result.Status.Should().Be("Done");
        result.Title.Should().Be("N/A");
        retryCallCount.Should().Be(3);
        serializerCallCount.Should().Be(1);
    }

    [Fact(DisplayName = "JiraIntegrationCore TryGetJiraTaskInfoAsync returns search http code when search endpoint fails.")]
    [Trait("Category", "Unit")]
    public async Task TryGetJiraTaskInfoAsyncReturnsSearchHttpCodeWhenSearchEndpointFails()
    {
        // Arrange
        var options = CreateOptions(retryCount: 2);
        var jiraTask = new JiraTaskReference("PROJ-1");
        var issueUrl = BuildIssueUrl(jiraTask);
        var api3SearchUrl = BuildApi3SearchUrl(jiraTask);
        using var httpClient = new HttpClient();
        var issueNotFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
        var api3FailedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        using var cts = new CancellationTokenSource();

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.JIRA)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == issueUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(issueNotFoundResponse);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == api3SearchUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(api3FailedResponse);

        var responseSerializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);

        var sut = new JiraIntegrationCore(
            httpClientFactoryMock.Object,
            options,
            retryExecutorMock.Object,
            responseSerializerMock.Object);

        // Act
        var result = await sut.TryGetJiraTaskInfoAsync(jiraTask, cts.Token);

        // Assert
        result.Status.Should().Be("HTTP 401");
        result.Title.Should().Be("N/A");
    }

    [Fact(DisplayName = "JiraIntegrationCore TryGetJiraTaskInfoAsync returns not found status when both searches return 404.")]
    [Trait("Category", "Unit")]
    public async Task TryGetJiraTaskInfoAsyncReturnsNotFoundStatusWhenBothSearchesReturn404()
    {
        // Arrange
        var options = CreateOptions(retryCount: 2);
        var jiraTask = new JiraTaskReference("PROJ-1");
        var issueUrl = BuildIssueUrl(jiraTask);
        var api3SearchUrl = BuildApi3SearchUrl(jiraTask);
        var api2SearchUrl = BuildApi2SearchUrl(jiraTask);
        using var httpClient = new HttpClient();
        var notFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
        using var cts = new CancellationTokenSource();

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.JIRA)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == issueUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(notFoundResponse);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == api3SearchUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(notFoundResponse);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == api2SearchUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(notFoundResponse);

        var responseSerializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);

        var sut = new JiraIntegrationCore(
            httpClientFactoryMock.Object,
            options,
            retryExecutorMock.Object,
            responseSerializerMock.Object);

        // Act
        var result = await sut.TryGetJiraTaskInfoAsync(jiraTask, cts.Token);

        // Assert
        result.Status.Should().Be("Not found");
        result.Title.Should().Be("N/A");
    }

    [Fact(DisplayName = "JiraIntegrationCore TryGetJiraTaskInfoAsync maps issue response payload.")]
    [Trait("Category", "Unit")]
    public async Task TryGetJiraTaskInfoAsyncMapsIssueResponsePayload()
    {
        // Arrange
        var options = CreateOptions(retryCount: 2);
        var jiraTask = new JiraTaskReference("PROJ-1");
        var issueUrl = BuildIssueUrl(jiraTask);
        using var httpClient = new HttpClient();
        var issueResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var issueDto = new JiraIssueStatusResponseDto
        {
            Fields = new JiraIssueFieldsDto
            {
                Status = new JiraStatusDto
                {
                    Name = "Done",
                },
                Summary = "Finish release",
                AdditionalFields = new Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["customfield_1"] = JsonElementFrom("{\"text\":\"update runbook\"}"),
                    ["customfield_2"] = JsonElementFrom("{\"text\":\"breaking API\"}"),
                },
            },
            Names = new Dictionary<string, string?>
            {
                ["customfield_1"] = "Required Actions",
                ["customfield_2"] = "Breaking changes",
            },
        };
        using var cts = new CancellationTokenSource();
        var serializerCallCount = 0;

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.JIRA)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == issueUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(issueResponse);

        var responseSerializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);
        responseSerializerMock
            .Setup(x => x.SerializeAsync<JiraIssueStatusResponseDto>(
                It.Is<HttpResponseMessage>(value => ReferenceEquals(value, issueResponse)),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .Callback(() => serializerCallCount++)
            .ReturnsAsync(issueDto);

        var sut = new JiraIntegrationCore(
            httpClientFactoryMock.Object,
            options,
            retryExecutorMock.Object,
            responseSerializerMock.Object);

        // Act
        var result = await sut.TryGetJiraTaskInfoAsync(jiraTask, cts.Token);

        // Assert
        result.Status.Should().Be("Done");
        result.Title.Should().Be("Finish release");
        result.RequiredActionsDetails.Should().Be("update runbook");
        result.BreakingChangesDetails.Should().Be("breaking API");
        serializerCallCount.Should().Be(1);
    }

    [Fact(DisplayName = "JiraIntegrationCore TryGetJiraTaskInfoAsync returns fallback values when issue payload is null.")]
    [Trait("Category", "Unit")]
    public async Task TryGetJiraTaskInfoAsyncReturnsFallbackValuesWhenIssuePayloadIsNull()
    {
        // Arrange
        var options = CreateOptions(retryCount: 2);
        var jiraTask = new JiraTaskReference("PROJ-1");
        var issueUrl = BuildIssueUrl(jiraTask);
        using var httpClient = new HttpClient();
        var issueResponse = new HttpResponseMessage(HttpStatusCode.OK);
        using var cts = new CancellationTokenSource();

        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.Is<string>(value => value == HttpClientNames.JIRA)))
            .Returns(httpClient);

        var retryExecutorMock = new Mock<IHttpRetryExecutor>(MockBehavior.Strict);
        retryExecutorMock
            .Setup(x => x.GetAsync(
                It.Is<HttpClient>(value => ReferenceEquals(value, httpClient)),
                It.Is<Uri>(value => value.OriginalString == issueUrl),
                It.Is<int>(value => value == 2),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync(issueResponse);

        var responseSerializerMock = new Mock<IResponseSerializer>(MockBehavior.Strict);
        responseSerializerMock
            .Setup(x => x.SerializeAsync<JiraIssueStatusResponseDto>(
                It.Is<HttpResponseMessage>(value => ReferenceEquals(value, issueResponse)),
                It.Is<CancellationToken>(value => value == cts.Token)))
            .ReturnsAsync((JiraIssueStatusResponseDto?)null);

        var sut = new JiraIntegrationCore(
            httpClientFactoryMock.Object,
            options,
            retryExecutorMock.Object,
            responseSerializerMock.Object);

        // Act
        var result = await sut.TryGetJiraTaskInfoAsync(jiraTask, cts.Token);

        // Assert
        result.Status.Should().Be("N/A");
        result.Title.Should().Be("N/A");
        result.RequiredActionsDetails.Should().BeNull();
        result.BreakingChangesDetails.Should().BeNull();
    }

    private static IOptions<AppSettings> CreateOptions(int retryCount)
    {
        var settings = new AppSettings
        {
            DevCsvFilePath = "components.csv",
            TargetCsvFilePath = "components.csv",
            Bitbucket = new BitbucketOptions
            {
                BaseUrl = new Uri("https://bitbucket.example.test/"),
                Workspace = "workspace",
                ProjectNames = ["PROJ"],
                AuthEmail = "bot@example.test",
                AuthApiToken = "token",
            },
            Jira = new JiraOptions
            {
                BaseUrl = new Uri("https://jira.example.test/"),
                RetryCount = retryCount,
                RequiredActionsFieldName = "Required Actions",
                BreakingChangesFieldName = "Breaking changes",
            },
        };

        return Options.Create(settings);
    }

    private static string BuildIssueUrl(JiraTaskReference jiraTask) =>
        $"rest/api/3/issue/{Uri.EscapeDataString(jiraTask.Value)}?expand=names";

    private static string BuildApi3SearchUrl(JiraTaskReference jiraTask)
    {
        var jql = $"key = \"{jiraTask.Value}\"";
        return $"rest/api/3/search/jql?jql={Uri.EscapeDataString(jql)}&fields=status&maxResults=1";
    }

    private static string BuildApi2SearchUrl(JiraTaskReference jiraTask)
    {
        var jql = $"key = \"{jiraTask.Value}\"";
        return $"rest/api/2/search?jql={Uri.EscapeDataString(jql)}&fields=status&maxResults=1";
    }

    private static System.Text.Json.JsonElement JsonElementFrom(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
