using System.Net;
using System.Net.Http.Headers;

using FluentAssertions;

using NReleaseBuilder.Transport;

namespace NReleaseBuilder.Tests.Transport;

public class HttpRetryExecutorTests
{
    [Fact(DisplayName = "HttpRetryExecutor GetAsync throws when http client is null.")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncThrowsWhenHttpClientIsNull()
    {
        // Arrange
        var sut = new HttpRetryExecutor();
        var requestUri = new Uri("https://example.test/resource");

        // Act
        Func<Task> action = () => sut.GetAsync(null!, requestUri, 1, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    [Fact(DisplayName = "HttpRetryExecutor GetAsync throws when url is null.")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncThrowsWhenUrlIsNull()
    {
        // Arrange
        var sut = new HttpRetryExecutor();
        using var httpClient = new HttpClient(new SequenceHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)));

        // Act
        Func<Task> action = () => sut.GetAsync(httpClient, null!, 1, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("url");
    }

    [Fact(DisplayName = "HttpRetryExecutor GetAsync returns response without retry for success.")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncReturnsResponseWithoutRetryForSuccess()
    {
        // Arrange
        var sut = new HttpRetryExecutor();
        var handler = new SequenceHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        var requestUri = new Uri("https://example.test/resource");

        // Act
        using var response = await sut.GetAsync(httpClient, requestUri, 3, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(1);
    }

    [Fact(DisplayName = "HttpRetryExecutor GetAsync retries transient status and returns next response.")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncRetriesTransientStatusAndReturnsNextResponse()
    {
        // Arrange
        var transient = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        transient.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
        var success = new HttpResponseMessage(HttpStatusCode.OK);

        var sut = new HttpRetryExecutor();
        var handler = new SequenceHttpMessageHandler(transient, success);
        using var httpClient = new HttpClient(handler);
        var requestUri = new Uri("https://example.test/resource");

        // Act
        using var response = await sut.GetAsync(httpClient, requestUri, 1, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(2);
    }

    [Fact(DisplayName = "HttpRetryExecutor GetAsync retries http request exception and succeeds.")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncRetriesHttpRequestExceptionAndSucceeds()
    {
        // Arrange
        var success = new HttpResponseMessage(HttpStatusCode.OK);

        var sut = new HttpRetryExecutor();
        var handler = new SequenceHttpMessageHandler(new HttpRequestException("network error"), success);
        using var httpClient = new HttpClient(handler);
        var requestUri = new Uri("https://example.test/resource");

        // Act
        using var response = await sut.GetAsync(httpClient, requestUri, 1, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(2);
    }

    [Fact(DisplayName = "HttpRetryExecutor GetAsync returns transient response when retry limit is reached.")]
    [Trait("Category", "Unit")]
    public async Task GetAsyncReturnsTransientResponseWhenRetryLimitIsReached()
    {
        // Arrange
        var firstTransient = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        firstTransient.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
        var secondTransient = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        secondTransient.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));

        var sut = new HttpRetryExecutor();
        var handler = new SequenceHttpMessageHandler(firstTransient, secondTransient);
        using var httpClient = new HttpClient(handler);
        var requestUri = new Uri("https://example.test/resource");

        // Act
        using var response = await sut.GetAsync(httpClient, requestUri, 1, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        handler.CallCount.Should().Be(2);
    }

    private sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<object> _steps;

        public SequenceHttpMessageHandler(params object[] steps)
        {
            if (steps is null || steps.Length == 0)
            {
                throw new ArgumentException("At least one step is required.", nameof(steps));
            }

            _steps = new Queue<object>(steps);
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

            if (_steps.Count == 0)
            {
                return Task.FromException<HttpResponseMessage>(
                    new InvalidOperationException("No response steps configured for handler."));
            }

            var nextStep = _steps.Dequeue();
            return nextStep switch
            {
                HttpResponseMessage response => Task.FromResult(response),
                Exception exception => Task.FromException<HttpResponseMessage>(exception),
                _ => Task.FromException<HttpResponseMessage>(
                    new InvalidOperationException("Unsupported handler step type.")),
            };
        }
    }
}
