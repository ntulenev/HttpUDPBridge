using FluentAssertions;

namespace Models.Tests;

public sealed class RecordModelsTests
{
    [Fact(DisplayName = "BridgeRequest initializes properties")]
    [Trait("Category", "Unit")]
    public void BridgeRequestInitializesProperties()
    {
        // Arrange
        const string requestId = "request-1";
        const string payload = "payload-1";

        // Act
        var model = new BridgeRequest(requestId, payload);

        // Assert
        model.RequestId.Should().Be(requestId);
        model.Payload.Should().Be(payload);
    }

    [Fact(DisplayName = "UdpRequestPacket initializes properties")]
    [Trait("Category", "Unit")]
    public void UdpRequestPacketInitializesProperties()
    {
        // Arrange
        const string requestId = "request-1";
        const string payload = "payload-1";

        // Act
        var model = new UdpRequestPacket(requestId, payload);

        // Assert
        model.RequestId.Should().Be(requestId);
        model.Payload.Should().Be(payload);
    }

    [Fact(DisplayName = "UdpResponsePacket initializes properties")]
    [Trait("Category", "Unit")]
    public void UdpResponsePacketInitializesProperties()
    {
        // Arrange
        const string requestId = "request-1";
        const string payload = "payload-1";
        var receivedAtUtc = new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero);

        // Act
        var model = new UdpResponsePacket(requestId, payload, receivedAtUtc);

        // Assert
        model.RequestId.Should().Be(requestId);
        model.Payload.Should().Be(payload);
        model.ReceivedAtUtc.Should().Be(receivedAtUtc);
    }

    [Fact(DisplayName = "CachedUdpResponse initializes properties")]
    [Trait("Category", "Unit")]
    public void CachedUdpResponseInitializesProperties()
    {
        // Arrange
        const string requestId = "request-1";
        const string payload = "payload-1";
        var receivedAtUtc = new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero);

        // Act
        var model = new CachedUdpResponse(requestId, payload, receivedAtUtc);

        // Assert
        model.RequestId.Should().Be(requestId);
        model.Payload.Should().Be(payload);
        model.ReceivedAtUtc.Should().Be(receivedAtUtc);
    }

    [Fact(DisplayName = "PendingRequestRegistration initializes properties")]
    [Trait("Category", "Unit")]
    public void PendingRequestRegistrationInitializesProperties()
    {
        // Arrange
        const string requestId = "request-1";
        var completion = Task.FromResult(PendingUdpRequestResult.NoResponse);
        const bool expectedIsOwner = true;

        // Act
        var model = new PendingRequestRegistration(requestId, completion, expectedIsOwner);

        // Assert
        model.RequestId.Should().Be(requestId);
        model.Completion.Should().Be(completion);
        model.IsOwner.Should().Be(expectedIsOwner);
    }
}
