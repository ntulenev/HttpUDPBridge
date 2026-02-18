using System.Net.Sockets;

using FluentAssertions;

using Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Models;

using Moq;

using Services.BackgroundService;

namespace Services.Tests;

public sealed class UdpResponseListenerServiceTests
{
    [Fact(DisplayName = "Constructor throws when transport is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenTransportIsNullThrowsArgumentNullException()
    {
        // Arrange
        IUdpTransport transport = null!;
        var requestRegistryMock = new Mock<IRequestRegistry>(MockBehavior.Strict);
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var logger = NullLogger<UdpResponseListenerService>.Instance;

        // Act
        Action act = () =>
            _ = new UdpResponseListenerService(
                transport,
                requestRegistryMock.Object,
                responseCacheMock.Object,
                logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when request registry is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenRequestRegistryIsNullThrowsArgumentNullException()
    {
        // Arrange
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        IRequestRegistry requestRegistry = null!;
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var logger = NullLogger<UdpResponseListenerService>.Instance;

        // Act
        Action act = () =>
            _ = new UdpResponseListenerService(
                transportMock.Object,
                requestRegistry,
                responseCacheMock.Object,
                logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when response cache is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenResponseCacheIsNullThrowsArgumentNullException()
    {
        // Arrange
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var requestRegistryMock = new Mock<IRequestRegistry>(MockBehavior.Strict);
        IResponseCache responseCache = null!;
        var logger = NullLogger<UdpResponseListenerService>.Instance;

        // Act
        Action act = () =>
            _ = new UdpResponseListenerService(
                transportMock.Object,
                requestRegistryMock.Object,
                responseCache,
                logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when logger is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenLoggerIsNullThrowsArgumentNullException()
    {
        // Arrange
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var requestRegistryMock = new Mock<IRequestRegistry>(MockBehavior.Strict);
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        ILogger<UdpResponseListenerService> logger = null!;

        // Act
        Action act = () =>
            _ = new UdpResponseListenerService(
                transportMock.Object,
                requestRegistryMock.Object,
                responseCacheMock.Object,
                logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Listener stores received response and completes registry")]
    [Trait("Category", "Unit")]
    public async Task ListenerStoresReceivedResponseAndCompletesRegistryAsync()
    {
        // Arrange
        var response = new UdpResponsePacket(
            "request-1",
            "payload-1",
            new DateTimeOffset(2026, 2, 18, 3, 0, 0, TimeSpan.Zero));
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var requestRegistryMock = new Mock<IRequestRegistry>(MockBehavior.Strict);
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var logger = NullLogger<UdpResponseListenerService>.Instance;
        var storedResponseSource = new TaskCompletionSource<CachedUdpResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completedResponseSource = new TaskCompletionSource<CachedUdpResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var receiveCallCount = 0;

        transportMock
            .Setup(transport => transport.ReceiveAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(cancellationToken =>
            {
                var current = Interlocked.Increment(ref receiveCallCount);
                return current == 1
                    ? Task.FromResult(response)
                    : WaitUntilCanceledAsync(cancellationToken);
            });
        responseCacheMock
            .Setup(cache => cache.Store(It.IsAny<CachedUdpResponse>()))
            .Callback<CachedUdpResponse>(cachedResponse =>
                storedResponseSource.TrySetResult(cachedResponse));
        requestRegistryMock
            .Setup(registry => registry.TryCompleteWithResponse(
                It.IsAny<string>(),
                It.IsAny<CachedUdpResponse>()))
            .Callback<string, CachedUdpResponse>((_, cachedResponse) =>
                completedResponseSource.TrySetResult(cachedResponse))
            .Returns(true);

        using var service = new UdpResponseListenerService(
            transportMock.Object,
            requestRegistryMock.Object,
            responseCacheMock.Object,
            logger);

        // Act
        await service.StartAsync(CancellationToken.None);
        var cachedResponse = await storedResponseSource.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        var completedResponse = await completedResponseSource.Task.WaitAsync(
            TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        // Assert
        cachedResponse.RequestId.Should().Be(response.RequestId);
        cachedResponse.Payload.Should().Be(response.Payload);
        cachedResponse.ReceivedAtUtc.Should().Be(response.ReceivedAtUtc);
        completedResponse.Should().BeSameAs(cachedResponse);
        receiveCallCount.Should().BeGreaterThanOrEqualTo(1);

        responseCacheMock.Verify(cache => cache.Store(It.IsAny<CachedUdpResponse>()), Times.Once);
        responseCacheMock.VerifyNoOtherCalls();
        requestRegistryMock.Verify(registry => registry.TryCompleteWithResponse(
            It.IsAny<string>(),
            It.IsAny<CachedUdpResponse>()), Times.Once);
        requestRegistryMock.VerifyNoOtherCalls();
        transportMock.Verify(transport => transport.ReceiveAsync(
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        transportMock.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "Listener continues after transient socket failure")]
    [Trait("Category", "Unit")]
    public async Task ListenerContinuesAfterTransientSocketFailureAsync()
    {
        // Arrange
        var response = new UdpResponsePacket(
            "request-1",
            "payload-1",
            new DateTimeOffset(2026, 2, 18, 4, 0, 0, TimeSpan.Zero));
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var requestRegistryMock = new Mock<IRequestRegistry>(MockBehavior.Strict);
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var logger = NullLogger<UdpResponseListenerService>.Instance;
        var storedResponseSource = new TaskCompletionSource<CachedUdpResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var receiveCallCount = 0;

        transportMock
            .Setup(transport => transport.ReceiveAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(cancellationToken =>
            {
                var current = Interlocked.Increment(ref receiveCallCount);
                return current switch
                {
                    1 => Task.FromException<UdpResponsePacket>(
                        new SocketException((int)SocketError.ConnectionReset)),
                    2 => Task.FromResult(response),
                    _ => WaitUntilCanceledAsync(cancellationToken)
                };
            });
        responseCacheMock
            .Setup(cache => cache.Store(It.IsAny<CachedUdpResponse>()))
            .Callback<CachedUdpResponse>(cachedResponse =>
                storedResponseSource.TrySetResult(cachedResponse));
        requestRegistryMock
            .Setup(registry => registry.TryCompleteWithResponse(
                It.IsAny<string>(),
                It.IsAny<CachedUdpResponse>()))
            .Returns(true);

        using var service = new UdpResponseListenerService(
            transportMock.Object,
            requestRegistryMock.Object,
            responseCacheMock.Object,
            logger);

        // Act
        await service.StartAsync(CancellationToken.None);
        var cachedResponse = await storedResponseSource.Task.WaitAsync(
            TimeSpan.FromSeconds(3));
        await service.StopAsync(CancellationToken.None);

        // Assert
        cachedResponse.Payload.Should().Be(response.Payload);
        receiveCallCount.Should().BeGreaterThanOrEqualTo(2);

        responseCacheMock.Verify(cache => cache.Store(It.IsAny<CachedUdpResponse>()), Times.Once);
        responseCacheMock.VerifyNoOtherCalls();
        requestRegistryMock.Verify(registry => registry.TryCompleteWithResponse(
            It.IsAny<string>(),
            It.IsAny<CachedUdpResponse>()), Times.Once);
        requestRegistryMock.VerifyNoOtherCalls();
        transportMock.Verify(transport => transport.ReceiveAsync(
            It.IsAny<CancellationToken>()), Times.AtLeast(2));
        transportMock.VerifyNoOtherCalls();
    }

    private static async Task<UdpResponsePacket> WaitUntilCanceledAsync(
        CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return default!;
    }
}
