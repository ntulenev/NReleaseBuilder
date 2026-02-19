using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

using NReleaseBuilder.Abstractions.Transport;

namespace NReleaseBuilder.Transport;

/// <summary>
/// Default HTTP retry executor with exponential backoff and jitter.
/// </summary>
public sealed class HttpRetryExecutor : IHttpRetryExecutor
{
    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetAsync(
        HttpClient httpClient,
        Uri url,
        int retryCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(url);

        var attempt = 0;

        while (true)
        {
            try
            {
                var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                if (attempt < retryCount && IsTransientStatusCode(response.StatusCode))
                {
                    var delay = GetRetryDelay(attempt + 1, response.Headers.RetryAfter);
                    response.Dispose();
                    attempt++;
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return response;
            }
            catch (HttpRequestException) when (attempt < retryCount)
            {
                attempt++;
                await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.Forbidden
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static TimeSpan GetRetryDelay(int attempt, RetryConditionHeaderValue? retryAfter = null)
    {
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return CapRetryDelay(delta);
        }

        if (retryAfter?.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                return CapRetryDelay(remaining);
            }
        }

        var milliseconds = Math.Min(10000, 300 * Math.Pow(2, attempt - 1));
        milliseconds += RandomNumberGenerator.GetInt32(100, 400);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static TimeSpan CapRetryDelay(TimeSpan value)
        => value > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : value;
}
