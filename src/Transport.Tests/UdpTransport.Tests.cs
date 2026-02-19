using FluentAssertions;

using Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Models;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Transport.Tests;

public sealed class UdpTransportTests
{
    [Fact(DisplayName = "Constructor throws when options is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsIsNullThrowsArgumentNullException()
    {
        // Arrange
        IOptions<UdpEndpointOptions> options = null!;
        var timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);
        var logger = NullLogger<UdpTransport>.Instance;

        // Act
        Action act = () => _ = new UdpTransport(options, timeProvider, logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when time provider is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenTimeProviderIsNullThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateOptions(localPort: 0, remotePort: GetFreeUdpPort());
        TimeProvider timeProvider = null!;
        var logger = NullLogger<UdpTransport>.Instance;

        // Act
        Action act = () => _ = new UdpTransport(options, timeProvider, logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when logger is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenLoggerIsNullThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateOptions(localPort: 0, remotePort: GetFreeUdpPort());
        var timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);
        ILogger<UdpTransport> logger = null!;

        // Act
        Action act = () => _ = new UdpTransport(options, timeProvider, logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "SendAsync throws when request is null")]
    [Trait("Category", "Unit")]
    public async Task SendAsyncWhenRequestIsNullThrowsArgumentNullException()
    {
        // Arrange
        using var transport = CreateTransport(localPort: 0, remotePort: GetFreeUdpPort());
        UdpRequestPacket request = null!;

        // Act
        Func<Task> act = () => transport.SendAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "SendAsync writes UDP wire message with requestId and payload")]
    [Trait("Category", "Unit")]
    public async Task SendAsyncWritesUdpWireMessageWithRequestIdAndPayload()
    {
        // Arrange
        var remotePort = GetFreeUdpPort();
        using var transport = CreateTransport(localPort: 0, remotePort: remotePort);
        using var receiver = new UdpClient(remotePort);
        var request = new UdpRequestPacket("request-1", "payload-1");
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // Act
        await transport.SendAsync(request, cancellationTokenSource.Token);
        var result = await receiver.ReceiveAsync(cancellationTokenSource.Token);
        using var document = JsonDocument.Parse(result.Buffer);

        // Assert
        document.RootElement.GetProperty("requestId").GetString().Should().Be("request-1");
        document.RootElement.GetProperty("payload").GetString().Should().Be("payload-1");
    }

    [Fact(DisplayName = "ReceiveAsync throws when token is already canceled")]
    [Trait("Category", "Unit")]
    public async Task ReceiveAsyncWhenTokenIsAlreadyCanceledThrowsOperationCanceledException()
    {
        // Arrange
        var localPort = GetFreeUdpPort();
        var remotePort = GetDifferentFreeUdpPort(localPort);
        using var transport = CreateTransport(localPort, remotePort);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        Func<Task> act = () => transport.ReceiveAsync(cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "ReceiveAsync parses valid response payload")]
    [Trait("Category", "Unit")]
    public async Task ReceiveAsyncParsesValidResponsePayload()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(utcNow);
        var localPort = GetFreeUdpPort();
        var remotePort = GetDifferentFreeUdpPort(localPort);
        using var transport = CreateTransport(localPort, remotePort, timeProvider);
        using var sender = new UdpClient(remotePort);
        sender.Connect(IPAddress.Loopback, localPort);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var payload = Encoding.UTF8.GetBytes("{\"requestId\":\"request-1\",\"payload\":\"payload-1\"}");

        // Act
        var receiveTask = transport.ReceiveAsync(cancellationTokenSource.Token);
        _ = await sender.SendAsync(payload, cancellationTokenSource.Token);
        var response = await receiveTask;

        // Assert
        response.RequestId.Should().Be("request-1");
        response.Payload.Should().Be("payload-1");
        response.ReceivedAtUtc.Should().Be(utcNow);
    }

    [Fact(DisplayName = "ReceiveAsync skips malformed response and returns next valid response")]
    [Trait("Category", "Unit")]
    public async Task ReceiveAsyncSkipsMalformedResponseAndReturnsNextValidResponse()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(utcNow);
        var localPort = GetFreeUdpPort();
        var remotePort = GetDifferentFreeUdpPort(localPort);
        using var transport = CreateTransport(localPort, remotePort, timeProvider);
        using var sender = new UdpClient(remotePort);
        sender.Connect(IPAddress.Loopback, localPort);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var malformedPayload = Encoding.UTF8.GetBytes("{\"requestId\":}");
        var validPayload = Encoding.UTF8.GetBytes("{\"requestId\":\"request-2\",\"payload\":null}");

        // Act
        var receiveTask = transport.ReceiveAsync(cancellationTokenSource.Token);
        _ = await sender.SendAsync(malformedPayload, cancellationTokenSource.Token);
        _ = await sender.SendAsync(validPayload, cancellationTokenSource.Token);
        var response = await receiveTask;

        // Assert
        response.RequestId.Should().Be("request-2");
        response.Payload.Should().Be(string.Empty);
        response.ReceivedAtUtc.Should().Be(utcNow);
    }

    private static IOptions<UdpEndpointOptions> CreateOptions(int localPort, int remotePort) =>
        Options.Create(new UdpEndpointOptions
        {
            RemoteHost = "127.0.0.1",
            RemotePort = remotePort,
            LocalPort = localPort
        });

    private static UdpTransport CreateTransport(
        int localPort,
        int remotePort,
        TimeProvider? timeProvider = null) =>
        new(
            CreateOptions(localPort, remotePort),
            timeProvider ?? new FixedTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<UdpTransport>.Instance);

    private static int GetFreeUdpPort()
    {
        using var udpClient = new UdpClient(0);
        return ((IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
    }

    private static int GetDifferentFreeUdpPort(int forbiddenPort)
    {
        while (true)
        {
            var port = GetFreeUdpPort();
            if (port != forbiddenPort)
            {
                return port;
            }
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        private readonly DateTimeOffset _utcNow;
    }
}
