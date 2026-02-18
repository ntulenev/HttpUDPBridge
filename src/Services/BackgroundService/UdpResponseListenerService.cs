using System.Net.Sockets;

using Abstractions;

using Microsoft.Extensions.Logging;

using Models;

namespace Services.BackgroundService;

/// <summary>
/// Listens for UDP responses and dispatches them to cache and pending request waiters.
/// </summary>
public sealed class UdpResponseListenerService : Microsoft.Extensions.Hosting.BackgroundService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UdpResponseListenerService"/> class.
    /// </summary>
    /// <param name="udpTransport">The UDP transport.</param>
    /// <param name="requestRegistry">The pending request registry.</param>
    /// <param name="responseCache">The response cache.</param>
    /// <param name="logger">The logger instance.</param>
    public UdpResponseListenerService(
        IUdpTransport udpTransport,
        IRequestRegistry requestRegistry,
        IResponseCache responseCache,
        ILogger<UdpResponseListenerService> logger)
    {
        ArgumentNullException.ThrowIfNull(udpTransport);
        ArgumentNullException.ThrowIfNull(requestRegistry);
        ArgumentNullException.ThrowIfNull(responseCache);
        ArgumentNullException.ThrowIfNull(logger);

        _udpTransport = udpTransport;
        _requestRegistry = requestRegistry;
        _responseCache = responseCache;
        _logger = logger;
    }

    /// <inheritdoc />
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var udpResponse = await _udpTransport.ReceiveAsync(stoppingToken)
                    .ConfigureAwait(false);

                var cachedResponse = new CachedUdpResponse(
                    udpResponse.RequestId,
                    udpResponse.Payload,
                    udpResponse.ReceivedAtUtc);

                _responseCache.Store(cachedResponse);
                _ = _requestRegistry.TryCompleteWithResponse(
                    cachedResponse.RequestId,
                    cachedResponse);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "UDP receive loop failed; retrying shortly.");
                await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogError(ex, "UDP receive loop failed; retrying shortly.");
                await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "UDP receive loop failed; retrying shortly.");
                await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private readonly IUdpTransport _udpTransport;
    private readonly IRequestRegistry _requestRegistry;
    private readonly IResponseCache _responseCache;
    private readonly ILogger<UdpResponseListenerService> _logger;
}
