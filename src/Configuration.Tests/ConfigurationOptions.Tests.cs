using FluentAssertions;

namespace Configuration.Tests;

public sealed class ConfigurationOptionsTests
{
    [Fact(DisplayName = "UdpEndpointOptions initializes all properties")]
    [Trait("Category", "Unit")]
    public void UdpEndpointOptionsInitializesAllProperties()
    {
        // Arrange
        const string expectedHost = "127.0.0.1";
        const int expectedRemotePort = 9000;
        const int expectedLocalPort = 10000;

        // Act
        var options = new UdpEndpointOptions
        {
            RemoteHost = expectedHost,
            RemotePort = expectedRemotePort,
            LocalPort = expectedLocalPort
        };

        // Assert
        options.RemoteHost.Should().Be(expectedHost);
        options.RemotePort.Should().Be(expectedRemotePort);
        options.LocalPort.Should().Be(expectedLocalPort);
    }

    [Fact(DisplayName = "UdpEndpointOptions local port defaults to zero")]
    [Trait("Category", "Unit")]
    public void UdpEndpointOptionsLocalPortDefaultsToZero()
    {
        // Arrange
        const string expectedHost = "localhost";
        const int expectedRemotePort = 9000;

        // Act
        var options = new UdpEndpointOptions
        {
            RemoteHost = expectedHost,
            RemotePort = expectedRemotePort
        };

        // Assert
        options.LocalPort.Should().Be(0);
    }

    [Fact(DisplayName = "UdpRetryOptions initializes all properties")]
    [Trait("Category", "Unit")]
    public void UdpRetryOptionsInitializesAllProperties()
    {
        // Arrange
        const int expectedAttemptTimeoutMilliseconds = 250;
        const int expectedMaxAttempts = 3;
        const int expectedDelayBetweenAttemptsMilliseconds = 50;
        const int expectedQueueCapacity = 1024;

        // Act
        var options = new UdpRetryOptions
        {
            AttemptTimeoutMilliseconds = expectedAttemptTimeoutMilliseconds,
            MaxAttempts = expectedMaxAttempts,
            DelayBetweenAttemptsMilliseconds = expectedDelayBetweenAttemptsMilliseconds,
            QueueCapacity = expectedQueueCapacity
        };

        // Assert
        options.AttemptTimeoutMilliseconds.Should().Be(expectedAttemptTimeoutMilliseconds);
        options.MaxAttempts.Should().Be(expectedMaxAttempts);
        options.DelayBetweenAttemptsMilliseconds.Should().Be(expectedDelayBetweenAttemptsMilliseconds);
        options.QueueCapacity.Should().Be(expectedQueueCapacity);
    }

    [Fact(DisplayName = "UdpRetryOptions delay defaults to zero")]
    [Trait("Category", "Unit")]
    public void UdpRetryOptionsDelayDefaultsToZero()
    {
        // Arrange
        const int expectedAttemptTimeoutMilliseconds = 250;
        const int expectedMaxAttempts = 3;
        const int expectedQueueCapacity = 64;

        // Act
        var options = new UdpRetryOptions
        {
            AttemptTimeoutMilliseconds = expectedAttemptTimeoutMilliseconds,
            MaxAttempts = expectedMaxAttempts,
            QueueCapacity = expectedQueueCapacity
        };

        // Assert
        options.DelayBetweenAttemptsMilliseconds.Should().Be(0);
    }

    [Fact(DisplayName = "HttpBridgeOptions initializes all properties")]
    [Trait("Category", "Unit")]
    public void HttpBridgeOptionsInitializesAllProperties()
    {
        // Arrange
        const int expectedTimeoutMilliseconds = 1000;
        const string expectedHeaderName = "X-Request-Id";

        // Act
        var options = new HttpBridgeOptions
        {
            RequestTimeoutMilliseconds = expectedTimeoutMilliseconds,
            RequestIdHeaderName = expectedHeaderName
        };

        // Assert
        options.RequestTimeoutMilliseconds.Should().Be(expectedTimeoutMilliseconds);
        options.RequestIdHeaderName.Should().Be(expectedHeaderName);
    }

    [Fact(DisplayName = "ResponseCacheOptions initializes all properties")]
    [Trait("Category", "Unit")]
    public void ResponseCacheOptionsInitializesAllProperties()
    {
        // Arrange
        const int expectedTimeToLiveSeconds = 300;
        const int expectedCleanupIntervalSeconds = 15;

        // Act
        var options = new ResponseCacheOptions
        {
            TimeToLiveSeconds = expectedTimeToLiveSeconds,
            CleanupIntervalSeconds = expectedCleanupIntervalSeconds
        };

        // Assert
        options.TimeToLiveSeconds.Should().Be(expectedTimeToLiveSeconds);
        options.CleanupIntervalSeconds.Should().Be(expectedCleanupIntervalSeconds);
    }
}
