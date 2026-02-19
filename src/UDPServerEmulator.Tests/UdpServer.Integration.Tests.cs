using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using FluentAssertions;

using UDPServerEmulator.Startup;

namespace UDPServerEmulator.Tests;

public sealed class UdpServerIntegrationTests
{
    [Fact(DisplayName = "UdpServer responds with configured prefix for valid datagrams")]
    [Trait("Category", "Integration")]
    public async Task UdpServerRespondsWithConfiguredPrefixForValidDatagramsAsync()
    {
        // Arrange
        var listenPort = ReserveUdpPort();
        var args = CreateArguments(listenPort, minDelayMilliseconds: 0, maxDelayMilliseconds: 0);
        using var app = StartupHelpers.CreateApplication(args);
        using var udpClient = new UdpClient(AddressFamily.InterNetwork);

        await app.StartAsync();

        try
        {
            await Task.Delay(50);

            var requestPayload = Encoding.UTF8.GetBytes(
                /*lang=json,strict*/"""{"requestId":"req-1","payload":"ping"}""");
            var endpoint = new IPEndPoint(IPAddress.Loopback, listenPort);
            _ = await udpClient.SendAsync(requestPayload, endpoint, CancellationToken.None);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var response = await udpClient.ReceiveAsync(cts.Token);
            using var responseJson = JsonDocument.Parse(response.Buffer);
            var responseRequestId = responseJson.RootElement.GetProperty("requestId").GetString();
            var responsePayload = responseJson.RootElement.GetProperty("payload").GetString();

            // Assert
            responseRequestId.Should().Be("req-1");
            responsePayload.Should().Be("emulator:ping");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact(DisplayName = "UdpServer ignores malformed payload and sends no response")]
    [Trait("Category", "Integration")]
    public async Task UdpServerIgnoresMalformedPayloadAndSendsNoResponseAsync()
    {
        // Arrange
        var listenPort = ReserveUdpPort();
        var args = CreateArguments(listenPort, minDelayMilliseconds: 0, maxDelayMilliseconds: 0);
        using var app = StartupHelpers.CreateApplication(args);
        using var udpClient = new UdpClient(AddressFamily.InterNetwork);

        await app.StartAsync();

        try
        {
            await Task.Delay(50);

            var malformedPayload = Encoding.UTF8.GetBytes("malformed-json");
            var endpoint = new IPEndPoint(IPAddress.Loopback, listenPort);
            _ = await udpClient.SendAsync(malformedPayload, endpoint, CancellationToken.None);

            // Act
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            Func<Task> act = async () =>
                _ = await udpClient.ReceiveAsync(cts.Token);

            // Assert
            _ = await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static string[] CreateArguments(
        int listenPort,
        int minDelayMilliseconds,
        int maxDelayMilliseconds) =>
        [
            $"--UdpServerEmulator:ListenPort={listenPort}",
            $"--UdpServerEmulator:MinDelayMilliseconds={minDelayMilliseconds}",
            $"--UdpServerEmulator:MaxDelayMilliseconds={maxDelayMilliseconds}",
            "--UdpServerEmulator:ResponsePrefix=emulator:"
        ];

    private static int ReserveUdpPort()
    {
        using var socket = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Dgram,
            ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = socket.LocalEndPoint as IPEndPoint;
        return endpoint?.Port
            ?? throw new InvalidOperationException("Failed to reserve UDP port.");
    }
}
