using System.Net.Sockets;

using FluentAssertions;

using Abstractions;

using Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Models;

using Moq;

using Services.Logic;

namespace Services.Tests;

public sealed class UdpRequestDispatcherTests
{
    [Fact(DisplayName = "Constructor throws when request registry is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenRequestRegistryIsNullThrowsArgumentNullException()
    {
        // Arrange
        IRequestRegistry requestRegistry = null!;
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var options = CreateOptions();
        var logger = NullLogger<UdpRequestDispatcher>.Instance;

        // Act
        Action act = () =>
            _ = new UdpRequestDispatcher(
                requestRegistry,
                transportMock.Object,
                options,
                logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when transport is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenTransportIsNullThrowsArgumentNullException()
    {
        // Arrange
        var requestRegistry = new RequestRegistry();
        IUdpTransport transport = null!;
        var options = CreateOptions();
        var logger = NullLogger<UdpRequestDispatcher>.Instance;

        // Act
        Action act = () =>
            _ = new UdpRequestDispatcher(
                requestRegistry,
                transport,
                options,
                logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when options is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsIsNullThrowsArgumentNullException()
    {
        // Arrange
        var requestRegistry = new RequestRegistry();
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        IOptions<UdpRetryOptions> options = null!;
        var logger = NullLogger<UdpRequestDispatcher>.Instance;

        // Act
        Action act = () =>
            _ = new UdpRequestDispatcher(
                requestRegistry,
                transportMock.Object,
                options,
                logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when logger is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenLoggerIsNullThrowsArgumentNullException()
    {
        // Arrange
        var requestRegistry = new RequestRegistry();
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var options = CreateOptions();
        ILogger<UdpRequestDispatcher> logger = null!;

        // Act
        Action act = () =>
            _ = new UdpRequestDispatcher(
                requestRegistry,
                transportMock.Object,
                options,
                logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "EnqueueAsync throws when request is null")]
    [Trait("Category", "Unit")]
    public async Task EnqueueAsyncWhenRequestIsNullThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(
            new RequestRegistry(),
            transportMock.Object,
            CreateOptions());
        BridgeRequest request = null!;
        var completion = Task.FromResult(PendingUdpRequestResult.NoResponse);

        // Act
        Func<Task> act = async () =>
            await dispatcher.EnqueueAsync(request, completion, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "EnqueueAsync throws when completion is null")]
    [Trait("Category", "Unit")]
    public async Task EnqueueAsyncWhenCompletionIsNullThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(
            new RequestRegistry(),
            transportMock.Object,
            CreateOptions());
        var request = new BridgeRequest("request-1", "payload-1");
        Task<PendingUdpRequestResult> completion = null!;

        // Act
        Func<Task> act = async () =>
            await dispatcher.EnqueueAsync(request, completion, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "RunAsync retries max attempts and completes without response")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncRetriesMaxAttemptsAndCompletesWithoutResponseAsync()
    {
        // Arrange
        var requestRegistry = new RequestRegistry();
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var options = CreateOptions(attemptTimeoutMs: 20, maxAttempts: 3, delayMs: 0);
        var dispatcher = CreateDispatcher(requestRegistry, transportMock.Object, options);
        const string requestId = "request-1";
        var registration = requestRegistry.Register(requestId);
        var request = new BridgeRequest(requestId, "payload-1");
        var sendCount = 0;

        transportMock
            .Setup(transport => transport.SendAsync(
                It.IsAny<UdpRequestPacket>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => sendCount++)
            .Returns(Task.CompletedTask);

        // Act
        var runTask = dispatcher.RunAsync(CancellationToken.None);
        await dispatcher.EnqueueAsync(request, registration.Completion, CancellationToken.None);
        var completion = await registration.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        dispatcher.Complete();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        completion.Should().Be(PendingUdpRequestResult.NoResponse);
        sendCount.Should().Be(3);
        transportMock.Verify(transport => transport.SendAsync(
            It.IsAny<UdpRequestPacket>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
        transportMock.VerifyNoOtherCalls();

        requestRegistry.Release(registration);
    }

    [Fact(DisplayName = "RunAsync stops retrying when request completes with response")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncStopsRetryingWhenRequestCompletesWithResponseAsync()
    {
        // Arrange
        var requestRegistry = new RequestRegistry();
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var options = CreateOptions(attemptTimeoutMs: 500, maxAttempts: 3, delayMs: 0);
        var dispatcher = CreateDispatcher(requestRegistry, transportMock.Object, options);
        const string requestId = "request-1";
        var registration = requestRegistry.Register(requestId);
        var request = new BridgeRequest(requestId, "payload-1");
        var response = new CachedUdpResponse(
            requestId,
            "response-1",
            new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero));
        var firstSendSource = new TaskCompletionSource<UdpRequestPacket>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sendCount = 0;

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
        await dispatcher.EnqueueAsync(request, registration.Completion, CancellationToken.None);
        _ = await firstSendSource.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _ = requestRegistry.TryCompleteWithResponse(requestId, response);
        var completion = await registration.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);
        dispatcher.Complete();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        sendCount.Should().Be(1);
        completion.HasResponse.Should().BeTrue();
        completion.Response.Should().Be(response);
        transportMock.Verify(transport => transport.SendAsync(
            It.IsAny<UdpRequestPacket>(),
            It.IsAny<CancellationToken>()), Times.Once);
        transportMock.VerifyNoOtherCalls();

        requestRegistry.Release(registration);
    }

    [Fact(DisplayName = "RunAsync continues retries when send throws socket exception")]
    [Trait("Category", "Unit")]
    public async Task RunAsyncContinuesRetriesWhenSendThrowsSocketExceptionAsync()
    {
        // Arrange
        var requestRegistry = new RequestRegistry();
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var options = CreateOptions(attemptTimeoutMs: 20, maxAttempts: 3, delayMs: 0);
        var dispatcher = CreateDispatcher(requestRegistry, transportMock.Object, options);
        const string requestId = "request-1";
        var registration = requestRegistry.Register(requestId);
        var request = new BridgeRequest(requestId, "payload-1");
        var sendCount = 0;

        transportMock
            .Setup(transport => transport.SendAsync(
                It.IsAny<UdpRequestPacket>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => sendCount++)
            .Returns(() => Task.FromException(
                new SocketException((int)SocketError.ConnectionReset)));

        // Act
        var runTask = dispatcher.RunAsync(CancellationToken.None);
        await dispatcher.EnqueueAsync(request, registration.Completion, CancellationToken.None);
        var completion = await registration.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        dispatcher.Complete();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        completion.Should().Be(PendingUdpRequestResult.NoResponse);
        sendCount.Should().Be(3);
        transportMock.Verify(transport => transport.SendAsync(
            It.IsAny<UdpRequestPacket>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
        transportMock.VerifyNoOtherCalls();

        requestRegistry.Release(registration);
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
