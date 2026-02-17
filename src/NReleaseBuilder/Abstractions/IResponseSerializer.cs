namespace NReleaseBuilder.Abstractions;

/// <summary>
/// Deserializes HTTP response content into typed objects.
/// </summary>
public interface IResponseSerializer
{
    /// <summary>
    /// Reads and deserializes response body JSON into the specified type.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <param name="response">HTTP response message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized object or <see langword="null"/> when body is empty or invalid.</returns>
    Task<T?> SerializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken);
}
