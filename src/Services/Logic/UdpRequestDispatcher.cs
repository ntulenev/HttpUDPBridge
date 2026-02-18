using System.Net.Sockets;
using System.Threading.Channels;

using Abstractions;

using Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Models;

namespace Services.Logic;

/// <summary>
/// Provides a bounded single-reader request channel for sequential UDP dispatching.
/// </summary>
public sealed class UdpRequestDispatcher
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UdpRequestDispatcher"/> class.
    /// </summary>
    /// <param name="requestRegistry">The pending request registry.</param>
    /// <param name="udpTransport">The UDP transport.</param>
    /// <param name="retryOptions">Retry behavior and queue options.</param>
    /// <param name="logger">The logger instance.</param>
    public UdpRequestDispatcher(
        IRequestRegistry requestRegistry,
        IUdpTransport udpTransport,
        IOptions<UdpRetryOptions> retryOptions,
        ILogger<UdpRequestDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(requestRegistry);
        ArgumentNullException.ThrowIfNull(udpTransport);
        ArgumentNullException.ThrowIfNull(retryOptions);
        ArgumentNullException.ThrowIfNull(logger);

        var optionsValue = retryOptions.Value;

        _requestRegistry = requestRegistry;
        _udpTransport = udpTransport;
        _attemptTimeout = TimeSpan.FromMilliseconds(optionsValue.AttemptTimeoutMilliseconds);
        _delayBetweenAttempts = TimeSpan.FromMilliseconds(
            optionsValue.DelayBetweenAttemptsMilliseconds);
        _maxAttempts = optionsValue.MaxAttempts;
        _logger = logger;

        _channel = Channel.CreateBounded<QueuedUdpRequest>(new BoundedChannelOptions(
            optionsValue.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    /// <summary>
    /// Enqueues a request for sequential processing.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <param name="completion">The completion task linked to the pending request.</param>
    /// <param name="cancellationToken">A cancellation token for backpressure waiting.</param>
    /// <returns>A task that completes when the request is queued.</returns>
    public async ValueTask EnqueueAsync(
        BridgeRequest request,
        Task<PendingUdpRequestResult> completion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(completion);

        var queueItem = new QueuedUdpRequest(request, completion);
        await _channel.Writer.WriteAsync(queueItem, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Processes queued requests sequentially until cancellation or queue completion.
    /// </summary>
    /// <param name="stoppingToken">The cancellation token used to stop processing.</param>
    /// <returns>A task that represents the background processing loop.</returns>
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (await _channel.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            while (_channel.Reader.TryRead(out var queueItem))
            {
                await ProcessRequestAsync(queueItem, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Completes the queue and prevents accepting new work items.
    /// </summary>
    public void Complete() => _ = _channel.Writer.TryComplete();

    private async Task ProcessRequestAsync(
        QueuedUdpRequest queueItem,
        CancellationToken stoppingToken)
    {
        try
        {
            if (queueItem.Completion.IsCompleted)
            {
                return;
            }

            var requestPacket = new UdpRequestPacket(
                queueItem.Request.RequestId,
                queueItem.Request.Payload);

            for (var attempt = 1; attempt <= _maxAttempts; attempt++)
            {
                await TrySendAsync(requestPacket, attempt, stoppingToken).ConfigureAwait(false);

                var completedTask = await Task.WhenAny(
                    queueItem.Completion,
                    Task.Delay(_attemptTimeout, stoppingToken)).ConfigureAwait(false);

                if (completedTask == queueItem.Completion)
                {
                    return;
                }

                if (attempt >= _maxAttempts || _delayBetweenAttempts <= TimeSpan.Zero)
                {
                    continue;
                }

                await Task.Delay(_delayBetweenAttempts, stoppingToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            _ = _requestRegistry.TryCompleteWithoutResponse(queueItem.Request.RequestId);
        }
    }

    private async Task TrySendAsync(
        UdpRequestPacket requestPacket,
        int attempt,
        CancellationToken stoppingToken)
    {
        try
        {
            await _udpTransport.SendAsync(requestPacket, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SocketException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send UDP request {RequestId} on attempt {Attempt}.",
                requestPacket.RequestId,
                attempt);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send UDP request {RequestId} on attempt {Attempt}.",
                requestPacket.RequestId,
                attempt);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send UDP request {RequestId} on attempt {Attempt}.",
                requestPacket.RequestId,
                attempt);
        }
    }

    private readonly Channel<QueuedUdpRequest> _channel;
    private readonly IRequestRegistry _requestRegistry;
    private readonly IUdpTransport _udpTransport;
    private readonly TimeSpan _attemptTimeout;
    private readonly TimeSpan _delayBetweenAttempts;
    private readonly int _maxAttempts;
    private readonly ILogger<UdpRequestDispatcher> _logger;

    private sealed record QueuedUdpRequest(
        BridgeRequest Request,
        Task<PendingUdpRequestResult> Completion);
}
