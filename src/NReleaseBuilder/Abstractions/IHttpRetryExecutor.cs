namespace NReleaseBuilder.Abstractions;

/// <summary>
/// Executes HTTP GET requests with retry semantics for transient failures.
/// </summary>
public interface IHttpRetryExecutor
{
    /// <summary>
    /// Sends an HTTP GET request with retries for transient status codes and network failures.
    /// </summary>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="url">Request URL.</param>
    /// <param name="retryCount">Maximum retry attempts after the initial request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP response message.</returns>
    Task<HttpResponseMessage> GetAsync(
        HttpClient httpClient,
        Uri url,
        int retryCount,
        CancellationToken cancellationToken);
}
