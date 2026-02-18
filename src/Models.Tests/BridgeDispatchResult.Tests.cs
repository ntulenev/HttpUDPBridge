using FluentAssertions;

namespace Models.Tests;

public sealed class BridgeDispatchResultTests
{
    [Fact(DisplayName = "Constructor initializes properties")]
    [Trait("Category", "Unit")]
    public void ConstructorInitializesProperties()
    {
        // Arrange
        const string requestId = "request-1";
        const string payload = "payload-1";
        var receivedAtUtc = new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = new BridgeDispatchResult(
            requestId,
            payload,
            receivedAtUtc,
            isTimeout: false,
            servedFromCache: true);

        // Assert
        result.RequestId.Should().Be(requestId);
        result.Payload.Should().Be(payload);
        result.ReceivedAtUtc.Should().Be(receivedAtUtc);
        result.IsTimeout.Should().BeFalse();
        result.ServedFromCache.Should().BeTrue();
    }

    [Fact(DisplayName = "Timeout creates timeout result")]
    [Trait("Category", "Unit")]
    public void TimeoutCreatesTimeoutResult()
    {
        // Arrange
        const string requestId = "request-1";

        // Act
        var result = BridgeDispatchResult.Timeout(requestId);

        // Assert
        result.RequestId.Should().Be(requestId);
        result.Payload.Should().BeNull();
        result.ReceivedAtUtc.Should().BeNull();
        result.IsTimeout.Should().BeTrue();
        result.ServedFromCache.Should().BeFalse();
    }

    [Fact(DisplayName = "FromCache throws when response is null")]
    [Trait("Category", "Unit")]
    public void FromCacheWhenResponseIsNullThrowsArgumentNullException()
    {
        // Arrange
        CachedUdpResponse response = null!;

        // Act
        Action act = () => _ = BridgeDispatchResult.FromCache(response);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "FromCache creates non-timeout cached result")]
    [Trait("Category", "Unit")]
    public void FromCacheCreatesNonTimeoutCachedResult()
    {
        // Arrange
        var response = new CachedUdpResponse(
            "request-1",
            "payload-1",
            new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero));

        // Act
        var result = BridgeDispatchResult.FromCache(response);

        // Assert
        result.RequestId.Should().Be(response.RequestId);
        result.Payload.Should().Be(response.Payload);
        result.ReceivedAtUtc.Should().Be(response.ReceivedAtUtc);
        result.IsTimeout.Should().BeFalse();
        result.ServedFromCache.Should().BeTrue();
    }

    [Fact(DisplayName = "FromLive throws when response is null")]
    [Trait("Category", "Unit")]
    public void FromLiveWhenResponseIsNullThrowsArgumentNullException()
    {
        // Arrange
        CachedUdpResponse response = null!;

        // Act
        Action act = () => _ = BridgeDispatchResult.FromLive(response);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "FromLive creates non-timeout live result")]
    [Trait("Category", "Unit")]
    public void FromLiveCreatesNonTimeoutLiveResult()
    {
        // Arrange
        var response = new CachedUdpResponse(
            "request-1",
            "payload-1",
            new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero));

        // Act
        var result = BridgeDispatchResult.FromLive(response);

        // Assert
        result.RequestId.Should().Be(response.RequestId);
        result.Payload.Should().Be(response.Payload);
        result.ReceivedAtUtc.Should().Be(response.ReceivedAtUtc);
        result.IsTimeout.Should().BeFalse();
        result.ServedFromCache.Should().BeFalse();
    }
}
