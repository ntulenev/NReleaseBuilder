using System.Text.Json;

using NReleaseBuilder.Abstractions.Transport;

namespace NReleaseBuilder.Transport;

/// <summary>
/// Default JSON response deserializer.
/// </summary>
public sealed class ResponseSerializer : IResponseSerializer
{
    /// <inheritdoc />
    public async Task<T?> SerializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(
            stream,
            _jsonSerializerOptions,
            cancellationToken).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
