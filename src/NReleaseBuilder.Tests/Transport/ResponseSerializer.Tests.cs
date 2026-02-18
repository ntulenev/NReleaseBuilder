using System.Net;
using System.Text;
using System.Text.Json;

using FluentAssertions;

using NReleaseBuilder.Transport;

namespace NReleaseBuilder.Tests.Transport;

public class ResponseSerializerTests
{
    [Fact(DisplayName = "ResponseSerializer SerializeAsync throws when response is null.")]
    [Trait("Category", "Unit")]
    public async Task SerializeAsyncThrowsWhenResponseIsNull()
    {
        // Arrange
        var sut = new ResponseSerializer();

        // Act
        Func<Task> action = () => sut.SerializeAsync<SamplePayload>(null!, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("response");
    }

    [Fact(DisplayName = "ResponseSerializer SerializeAsync maps case-insensitive property names.")]
    [Trait("Category", "Unit")]
    public async Task SerializeAsyncMapsCaseInsensitivePropertyNames()
    {
        // Arrange
        var sut = new ResponseSerializer();
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(/*lang=json,strict*/ "{\"NAME\":\"worker\"}", Encoding.UTF8, "application/json"),
        };

        // Act
        var result = await sut.SerializeAsync<SamplePayload>(response, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("worker");
    }

    [Fact(DisplayName = "ResponseSerializer SerializeAsync returns null when payload is JSON null.")]
    [Trait("Category", "Unit")]
    public async Task SerializeAsyncReturnsNullWhenPayloadIsJsonNull()
    {
        // Arrange
        var sut = new ResponseSerializer();
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json"),
        };

        // Act
        var result = await sut.SerializeAsync<SamplePayload>(response, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "ResponseSerializer SerializeAsync throws for invalid JSON payload.")]
    [Trait("Category", "Unit")]
    public async Task SerializeAsyncThrowsForInvalidJsonPayload()
    {
        // Arrange
        var sut = new ResponseSerializer();
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{invalid-json}", Encoding.UTF8, "application/json"),
        };

        // Act
        Func<Task> action = () => sut.SerializeAsync<SamplePayload>(response, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<JsonException>();
    }

    private sealed class SamplePayload
    {
        public string? Name { get; init; }
    }
}
