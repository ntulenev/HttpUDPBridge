using FluentAssertions;

namespace Models.Tests;

public sealed class PendingUdpRequestResultTests
{
    [Fact(DisplayName = "Constructor initializes properties")]
    [Trait("Category", "Unit")]
    public void ConstructorInitializesProperties()
    {
        // Arrange
        var response = new CachedUdpResponse(
            "request-1",
            "payload-1",
            new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero));

        // Act
        var result = new PendingUdpRequestResult(true, response);

        // Assert
        result.HasResponse.Should().BeTrue();
        result.Response.Should().Be(response);
    }

    [Fact(DisplayName = "NoResponse contains no response payload")]
    [Trait("Category", "Unit")]
    public void NoResponseContainsNoResponsePayload()
    {
        // Arrange & Act
        var result = PendingUdpRequestResult.NoResponse;

        // Assert
        result.HasResponse.Should().BeFalse();
        result.Response.Should().BeNull();
    }

    [Fact(DisplayName = "WithResponse throws when response is null")]
    [Trait("Category", "Unit")]
    public void WithResponseWhenResponseIsNullThrowsArgumentNullException()
    {
        // Arrange
        CachedUdpResponse response = null!;

        // Act
        Action act = () => _ = PendingUdpRequestResult.WithResponse(response);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "WithResponse creates result with response")]
    [Trait("Category", "Unit")]
    public void WithResponseCreatesResultWithResponse()
    {
        // Arrange
        var response = new CachedUdpResponse(
            "request-1",
            "payload-1",
            new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero));

        // Act
        var result = PendingUdpRequestResult.WithResponse(response);

        // Assert
        result.HasResponse.Should().BeTrue();
        result.Response.Should().Be(response);
    }
}
