using System.Net.Sockets;
using System.Text.Json;

using Abstractions;

using Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Models;

using Transport.Serialization;

namespace Transport;

/// <summary>
/// Provides UDP send/receive operations for the bridge using a single shared socket.
/// </summary>
public sealed class UdpTransport : IUdpTransport, IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UdpTransport"/> class.
    /// </summary>
    /// <param name="options">UDP endpoint configuration options.</param>
    /// <param name="timeProvider">A time provider used to stamp received responses.</param>
    /// <param name="logger">The logger instance.</param>
    public UdpTransport(
        IOptions<UdpEndpointOptions> options,
        TimeProvider timeProvider,
        ILogger<UdpTransport> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        var udpClient = CreateClient(options.Value);

        _udpClient = udpClient;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(UdpRequestPacket request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var message = new UdpWireMessage(request.RequestId, request.Payload);
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            message,
            UdpWireJsonSerializerContext.Default.UdpWireMessage);

        await _sendSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _ = await _udpClient.SendAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _sendSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<UdpResponsePacket> ReceiveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await _udpClient.ReceiveAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!TryParseResponse(result.Buffer, out var response))
            {
                _logger.LogWarning(
                    "Ignoring malformed UDP message with payload length {Length}.",
                    result.Buffer.Length);
                continue;
            }

            return response;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _sendSemaphore.Dispose();
        _udpClient.Dispose();
    }

    private bool TryParseResponse(
        byte[] payload,
        out UdpResponsePacket response)
    {
        try
        {
            var message = JsonSerializer.Deserialize(
                payload,
                UdpWireJsonSerializerContext.Default.UdpWireMessage);

            if (message is null || string.IsNullOrWhiteSpace(message.RequestId))
            {
                response = default!;
                return false;
            }

            response = new UdpResponsePacket(
                message.RequestId,
                message.Payload ?? string.Empty,
                _timeProvider.GetUtcNow());

            return true;
        }
        catch (JsonException)
        {
            response = default!;
            return false;
        }
    }

    private static UdpClient CreateClient(UdpEndpointOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var client = options.LocalPort > 0
            ? new UdpClient(options.LocalPort)
            : new UdpClient();

        try
        {
            client.Connect(options.RemoteHost, options.RemotePort);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private readonly UdpClient _udpClient;
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UdpTransport> _logger;
}
