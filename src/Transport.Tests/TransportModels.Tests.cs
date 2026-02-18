using FluentAssertions;

using System.Text.Json;

namespace Transport.Tests;

public sealed class TransportModelsTests
{
    private static readonly JsonSerializerOptions CamelCaseJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact(DisplayName = "Deserialize reads camelCase payload for BridgeHttpRequest")]
    [Trait("Category", "Unit")]
    public void DeserializeReadsCamelCasePayloadForBridgeHttpRequest()
    {
        // Arrange
        const string json = "{\"payload\":\"hello\"}";

        // Act
        var result = JsonSerializer.Deserialize<BridgeHttpRequest>(json, CamelCaseJsonSerializerOptions);

        // Assert
        result.Should().NotBeNull();
        result!.Payload.Should().Be("hello");
    }

    [Fact(DisplayName = "Serialize writes camelCase properties for BridgeHttpResponse")]
    [Trait("Category", "Unit")]
    public void SerializeWritesCamelCasePropertiesForBridgeHttpResponse()
    {
        // Arrange
        var receivedAtUtc = new DateTimeOffset(2026, 2, 18, 22, 13, 30, TimeSpan.Zero);
        var model = new BridgeHttpResponse(
            "request-1",
            "payload-1",
            true,
            receivedAtUtc);

        // Act
        var json = JsonSerializer.Serialize(model, CamelCaseJsonSerializerOptions);
        using var document = JsonDocument.Parse(json);

        // Assert
        document.RootElement.GetProperty("requestId").GetString().Should().Be("request-1");
        document.RootElement.GetProperty("payload").GetString().Should().Be("payload-1");
        document.RootElement.GetProperty("fromCache").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("receivedAtUtc").GetDateTimeOffset()
            .Should()
            .Be(receivedAtUtc);
    }

    [Fact(DisplayName = "Serialize writes camelCase properties for BridgeTimeoutResponse")]
    [Trait("Category", "Unit")]
    public void SerializeWritesCamelCasePropertiesForBridgeTimeoutResponse()
    {
        // Arrange
        var model = new BridgeTimeoutResponse("request-1", "timeout");

        // Act
        var json = JsonSerializer.Serialize(model, CamelCaseJsonSerializerOptions);

        // Assert
        json.Should().Be("{\"requestId\":\"request-1\",\"error\":\"timeout\"}");
    }
}
