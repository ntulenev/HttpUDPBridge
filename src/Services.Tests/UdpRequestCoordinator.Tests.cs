using FluentAssertions;

using Abstractions;

using Configuration;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Models;

using Moq;

using Services.Logic;

namespace Services.Tests;

public sealed class UdpRequestCoordinatorTests
{
    [Fact(DisplayName = "Constructor throws when request registry is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenRequestRegistryIsNullThrowsArgumentNullException()
    {
        // Arrange
        IRequestRegistry requestRegistry = null!;
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(
            new RequestRegistry(),
            transportMock.Object,
            CreateOptions());

        // Act
        Action act = () =>
            _ = new UdpRequestCoordinator(
                requestRegistry,
                responseCacheMock.Object,
                dispatcher);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when response cache is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenResponseCacheIsNullThrowsArgumentNullException()
    {
        // Arrange
        var requestRegistry = new RequestRegistry();
        IResponseCache responseCache = null!;
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(
            requestRegistry,
            transportMock.Object,
            CreateOptions());

        // Act
        Action act = () =>
            _ = new UdpRequestCoordinator(
                requestRegistry,
                responseCache,
                dispatcher);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when dispatcher is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenDispatcherIsNullThrowsArgumentNullException()
    {
        // Arrange
        var requestRegistry = new RequestRegistry();
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        UdpRequestDispatcher dispatcher = null!;

        // Act
        Action act = () =>
            _ = new UdpRequestCoordinator(
                requestRegistry,
                responseCacheMock.Object,
                dispatcher);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "DispatchAsync throws when request is null")]
    [Trait("Category", "Unit")]
    public async Task DispatchAsyncWhenRequestIsNullThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        var requestRegistry = new RequestRegistry();
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(
            requestRegistry,
            transportMock.Object,
            CreateOptions());
        var coordinator = new UdpRequestCoordinator(
            requestRegistry,
            responseCacheMock.Object,
            dispatcher);
        BridgeRequest request = null!;

        // Act
        Func<Task> act = async () =>
            _ = await coordinator.DispatchAsync(
                request,
                TimeSpan.FromSeconds(1),
                CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "DispatchAsync returns cached response when available")]
    [Trait("Category", "Unit")]
    public async Task DispatchAsyncReturnsCachedResponseWhenAvailableAsync()
    {
        // Arrange
        const string requestId = "request-1";
        var cachedResponse = new CachedUdpResponse(
            requestId,
            "payload-cached",
            new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero));

        var requestRegistryMock = new Mock<IRequestRegistry>(MockBehavior.Strict);
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(
            requestRegistryMock.Object,
            transportMock.Object,
            CreateOptions());
        var coordinator = new UdpRequestCoordinator(
            requestRegistryMock.Object,
            responseCacheMock.Object,
            dispatcher);
        var request = new BridgeRequest(requestId, "payload-live");
        CachedUdpResponse? cacheHit = cachedResponse;

        responseCacheMock
            .Setup(cache => cache.TryGet(requestId, out cacheHit))
            .Returns(true);

        // Act
        var result = await coordinator.DispatchAsync(
            request,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        // Assert
        result.IsTimeout.Should().BeFalse();
        result.ServedFromCache.Should().BeTrue();
        result.RequestId.Should().Be(cachedResponse.RequestId);
        result.Payload.Should().Be(cachedResponse.Payload);
        result.ReceivedAtUtc.Should().Be(cachedResponse.ReceivedAtUtc);

        responseCacheMock.Verify(cache => cache.TryGet(requestId, out cacheHit), Times.Once);
        responseCacheMock.VerifyNoOtherCalls();
        requestRegistryMock.VerifyNoOtherCalls();
        transportMock.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "DispatchAsync returns live response when request completes")]
    [Trait("Category", "Unit")]
    public async Task DispatchAsyncReturnsLiveResponseWhenRequestCompletesAsync()
    {
        // Arrange
        const string requestId = "request-1";
        var requestRegistry = new RequestRegistry();
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(
            requestRegistry,
            transportMock.Object,
            CreateOptions(attemptTimeoutMs: 500, maxAttempts: 3));
        var coordinator = new UdpRequestCoordinator(
            requestRegistry,
            responseCacheMock.Object,
            dispatcher);
        var request = new BridgeRequest(requestId, "payload-live");
        var liveResponse = new CachedUdpResponse(
            requestId,
            "udp-response",
            new DateTimeOffset(2026, 2, 18, 1, 0, 0, TimeSpan.Zero));
        CachedUdpResponse? cacheMiss = null;
        var firstSendSource = new TaskCompletionSource<UdpRequestPacket>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sendCount = 0;

        responseCacheMock
            .Setup(cache => cache.TryGet(requestId, out cacheMiss))
            .Returns(false);
        transportMock
            .Setup(transport => transport.SendAsync(
                It.IsAny<UdpRequestPacket>(),
                It.IsAny<CancellationToken>()))
            .Callback<UdpRequestPacket, CancellationToken>((packet, _) =>
            {
                sendCount++;
                firstSendSource.TrySetResult(packet);
            })
            .Returns(Task.CompletedTask);

        // Act
        var runTask = dispatcher.RunAsync(CancellationToken.None);
        var dispatchTask = coordinator.DispatchAsync(
            request,
            TimeSpan.FromSeconds(2),
            CancellationToken.None);
        _ = await firstSendSource.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _ = requestRegistry.TryCompleteWithResponse(requestId, liveResponse);
        var result = await dispatchTask;
        dispatcher.Complete();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        result.IsTimeout.Should().BeFalse();
        result.ServedFromCache.Should().BeFalse();
        result.RequestId.Should().Be(liveResponse.RequestId);
        result.Payload.Should().Be(liveResponse.Payload);
        result.ReceivedAtUtc.Should().Be(liveResponse.ReceivedAtUtc);
        sendCount.Should().Be(1);

        responseCacheMock.Verify(cache => cache.TryGet(requestId, out cacheMiss), Times.Once);
        responseCacheMock.VerifyNoOtherCalls();
        transportMock.Verify(transport => transport.SendAsync(
            It.IsAny<UdpRequestPacket>(),
            It.IsAny<CancellationToken>()), Times.Once);
        transportMock.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "DispatchAsync returns timeout when retries are exhausted")]
    [Trait("Category", "Unit")]
    public async Task DispatchAsyncReturnsTimeoutWhenRetriesAreExhaustedAsync()
    {
        // Arrange
        const string requestId = "request-1";
        var requestRegistry = new RequestRegistry();
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(
            requestRegistry,
            transportMock.Object,
            CreateOptions(attemptTimeoutMs: 20, maxAttempts: 1));
        var coordinator = new UdpRequestCoordinator(
            requestRegistry,
            responseCacheMock.Object,
            dispatcher);
        var request = new BridgeRequest(requestId, "payload-1");
        CachedUdpResponse? cacheMiss = null;
        var sendCount = 0;

        responseCacheMock
            .Setup(cache => cache.TryGet(requestId, out cacheMiss))
            .Returns(false);
        transportMock
            .Setup(transport => transport.SendAsync(
                It.IsAny<UdpRequestPacket>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => sendCount++)
            .Returns(Task.CompletedTask);

        // Act
        var runTask = dispatcher.RunAsync(CancellationToken.None);
        var result = await coordinator.DispatchAsync(
            request,
            TimeSpan.FromSeconds(2),
            CancellationToken.None);
        dispatcher.Complete();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        result.IsTimeout.Should().BeTrue();
        result.ServedFromCache.Should().BeFalse();
        result.Payload.Should().BeNull();
        result.ReceivedAtUtc.Should().BeNull();
        sendCount.Should().Be(1);

        responseCacheMock.Verify(cache => cache.TryGet(requestId, out cacheMiss), Times.Once);
        responseCacheMock.VerifyNoOtherCalls();
        transportMock.Verify(transport => transport.SendAsync(
            It.IsAny<UdpRequestPacket>(),
            It.IsAny<CancellationToken>()), Times.Once);
        transportMock.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "DispatchAsync sends once for concurrent calls with same request id")]
    [Trait("Category", "Unit")]
    public async Task DispatchAsyncSendsOnceForConcurrentCallsWithSameRequestIdAsync()
    {
        // Arrange
        const string requestId = "request-1";
        var requestRegistry = new RequestRegistry();
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(
            requestRegistry,
            transportMock.Object,
            CreateOptions(attemptTimeoutMs: 500, maxAttempts: 3));
        var coordinator = new UdpRequestCoordinator(
            requestRegistry,
            responseCacheMock.Object,
            dispatcher);
        var request = new BridgeRequest(requestId, "payload-1");
        var sharedResponse = new CachedUdpResponse(
            requestId,
            "udp-response",
            new DateTimeOffset(2026, 2, 18, 2, 0, 0, TimeSpan.Zero));
        CachedUdpResponse? cacheMiss = null;
        var firstSendSource = new TaskCompletionSource<UdpRequestPacket>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sendCount = 0;

        responseCacheMock
            .Setup(cache => cache.TryGet(requestId, out cacheMiss))
            .Returns(false);
        transportMock
            .Setup(transport => transport.SendAsync(
                It.IsAny<UdpRequestPacket>(),
                It.IsAny<CancellationToken>()))
            .Callback<UdpRequestPacket, CancellationToken>((packet, _) =>
            {
                sendCount++;
                firstSendSource.TrySetResult(packet);
            })
            .Returns(Task.CompletedTask);

        // Act
        var runTask = dispatcher.RunAsync(CancellationToken.None);
        var firstTask = coordinator.DispatchAsync(
            request,
            TimeSpan.FromSeconds(2),
            CancellationToken.None);
        var secondTask = coordinator.DispatchAsync(
            request,
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        _ = await firstSendSource.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(20);
        _ = requestRegistry.TryCompleteWithResponse(requestId, sharedResponse);

        var first = await firstTask;
        var second = await secondTask;
        dispatcher.Complete();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        sendCount.Should().Be(1);
        first.IsTimeout.Should().BeFalse();
        second.IsTimeout.Should().BeFalse();
        first.Payload.Should().Be(sharedResponse.Payload);
        second.Payload.Should().Be(sharedResponse.Payload);

        responseCacheMock.Verify(cache => cache.TryGet(requestId, out cacheMiss), Times.Exactly(2));
        responseCacheMock.VerifyNoOtherCalls();
        transportMock.Verify(transport => transport.SendAsync(
            It.IsAny<UdpRequestPacket>(),
            It.IsAny<CancellationToken>()), Times.Once);
        transportMock.VerifyNoOtherCalls();
    }

    private static IOptions<UdpRetryOptions> CreateOptions(
        int attemptTimeoutMs = 50,
        int maxAttempts = 3,
        int delayMs = 0,
        int queueCapacity = 32) =>
        Options.Create(new UdpRetryOptions
        {
            AttemptTimeoutMilliseconds = attemptTimeoutMs,
            MaxAttempts = maxAttempts,
            DelayBetweenAttemptsMilliseconds = delayMs,
            QueueCapacity = queueCapacity
        });

    private static UdpRequestDispatcher CreateDispatcher(
        IRequestRegistry requestRegistry,
        IUdpTransport transport,
        IOptions<UdpRetryOptions> options) =>
        new(
            requestRegistry,
            transport,
            options,
            NullLogger<UdpRequestDispatcher>.Instance);
}
