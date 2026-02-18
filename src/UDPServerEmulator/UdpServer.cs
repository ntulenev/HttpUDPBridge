using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using UDPServerEmulator.Configuration;
using UDPServerEmulator.Serialization;

namespace UDPServerEmulator;

/// <summary>
/// A simple UDP server emulator that echoes payloads after a random delay.
/// </summary>
internal sealed class UdpServer : BackgroundService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UdpServer"/> class.
    /// </summary>
    /// <param name="options">The emulator options.</param>
    /// <param name="logger">The logger instance.</param>
    public UdpServer(
        IOptions<UdpServerEmulatorOptions> options,
        ILogger<UdpServer> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var optionsValue = options.Value;
        _listenPort = optionsValue.ListenPort;
        _minDelayMilliseconds = optionsValue.MinDelayMilliseconds;
        _maxDelayMilliseconds = optionsValue.MaxDelayMilliseconds;
        _responsePrefix = optionsValue.ResponsePrefix;
        _logger = logger;
    }

    /// <inheritdoc />
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udpClient = new UdpClient(_listenPort);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "UDP emulator started on port {Port} with delay range {MinDelay}-{MaxDelay} ms.",
                _listenPort,
                _minDelayMilliseconds,
                _maxDelayMilliseconds);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            UdpReceiveResult receiveResult;

            try
            {
                receiveResult = await udpClient.ReceiveAsync(stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (!TryParseMessage(receiveResult.Buffer, out var requestMessage))
            {
                _logger.LogWarning(
                    "Ignoring malformed UDP payload of {Length} bytes.",
                    receiveResult.Buffer.Length);
                continue;
            }

            var delayMilliseconds = GetRandomDelayMilliseconds();
            if (delayMilliseconds > 0)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(delayMilliseconds),
                    stoppingToken).ConfigureAwait(false);
            }

            var responseMessage = new UdpWireMessage(
                requestMessage.RequestId,
                string.Concat(_responsePrefix, requestMessage.Payload ?? string.Empty));

            var responsePayload = JsonSerializer.SerializeToUtf8Bytes(
                responseMessage,
                UdpWireJsonSerializerContext.Default.UdpWireMessage);

            _ = await udpClient.SendAsync(
                responsePayload,
                receiveResult.RemoteEndPoint,
                stoppingToken).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Responded to request {RequestId} in {Delay} ms.",
                    requestMessage.RequestId,
                    delayMilliseconds);
            }
        }
    }

    private int GetRandomDelayMilliseconds()
    {
        if (_minDelayMilliseconds == _maxDelayMilliseconds)
        {
            return _minDelayMilliseconds;
        }

        return RandomNumberGenerator.GetInt32(
            _minDelayMilliseconds,
            _maxDelayMilliseconds + 1);
    }

    private static bool TryParseMessage(
        byte[] payload,
        out UdpWireMessage message)
    {
        try
        {
            var parsedMessage = JsonSerializer.Deserialize(
                payload,
                UdpWireJsonSerializerContext.Default.UdpWireMessage);

            if (parsedMessage is null || string.IsNullOrWhiteSpace(parsedMessage.RequestId))
            {
                message = default!;
                return false;
            }

            message = new UdpWireMessage(
                parsedMessage.RequestId,
                parsedMessage.Payload ?? string.Empty);

            return true;
        }
        catch (JsonException)
        {
            message = default!;
            return false;
        }
    }

    private readonly int _listenPort;
    private readonly int _minDelayMilliseconds;
    private readonly int _maxDelayMilliseconds;
    private readonly string _responsePrefix;
    private readonly ILogger<UdpServer> _logger;
}
