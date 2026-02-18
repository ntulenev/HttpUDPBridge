using System.Threading.Channels;

using FluentAssertions;

using Abstractions;

using Configuration;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Models;

using Moq;

using Services.BackgroundService;
using Services.Logic;

namespace Services.Tests;

public sealed class UdpRequestDispatcherServiceTests
{
    [Fact(DisplayName = "Constructor throws when dispatcher is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenDispatcherIsNullThrowsArgumentNullException()
    {
        // Arrange
        UdpRequestDispatcher dispatcher = null!;

        // Act
        Action act = () => _ = new UdpRequestDispatcherService(dispatcher);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "StopAsync completes dispatcher queue")]
    [Trait("Category", "Unit")]
    public async Task StopAsyncCompletesDispatcherQueueAsync()
    {
        // Arrange
        var requestRegistry = new RequestRegistry();
        var transportMock = new Mock<IUdpTransport>(MockBehavior.Strict);
        var dispatcher = new UdpRequestDispatcher(
            requestRegistry,
            transportMock.Object,
            CreateOptions(),
            NullLogger<UdpRequestDispatcher>.Instance);
        using var service = new UdpRequestDispatcherService(dispatcher);
        var request = new BridgeRequest("request-1", "payload-1");
        var completion = Task.FromResult(PendingUdpRequestResult.NoResponse);

        // Act
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Func<Task> act = async () =>
            await dispatcher.EnqueueAsync(request, completion, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ChannelClosedException>();
        transportMock.VerifyNoOtherCalls();
    }

    private static IOptions<UdpRetryOptions> CreateOptions() =>
        Options.Create(new UdpRetryOptions
        {
            AttemptTimeoutMilliseconds = 50,
            MaxAttempts = 1,
            DelayBetweenAttemptsMilliseconds = 0,
            QueueCapacity = 8
        });
}
