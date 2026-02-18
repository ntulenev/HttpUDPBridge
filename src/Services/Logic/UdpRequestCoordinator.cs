using Abstractions;

using Models;

namespace Services.Logic;

/// <summary>
/// Coordinates HTTP request lifecycles with UDP queue dispatch behavior.
/// </summary>
public sealed class UdpRequestCoordinator : IUdpRequestCoordinator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UdpRequestCoordinator"/> class.
    /// </summary>
    /// <param name="requestRegistry">The pending request registry.</param>
    /// <param name="responseCache">The in-memory response cache.</param>
    /// <param name="requestDispatchQueue">The bounded request dispatch queue.</param>
    public UdpRequestCoordinator(
        IRequestRegistry requestRegistry,
        IResponseCache responseCache,
        UdpRequestDispatcher requestDispatchQueue)
    {
        ArgumentNullException.ThrowIfNull(requestRegistry);
        ArgumentNullException.ThrowIfNull(responseCache);
        ArgumentNullException.ThrowIfNull(requestDispatchQueue);

        _requestRegistry = requestRegistry;
        _responseCache = responseCache;
        _requestDispatchQueue = requestDispatchQueue;
    }

    /// <inheritdoc />
    public async Task<BridgeDispatchResult> DispatchAsync(
        BridgeRequest request,
        TimeSpan httpTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_responseCache.TryGet(request.RequestId, out var cachedResponse))
        {
            return BridgeDispatchResult.FromCache(cachedResponse);
        }

        var registration = _requestRegistry.Register(request.RequestId);
        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(httpTimeout);

        try
        {
            if (registration.IsOwner)
            {
                try
                {
                    await _requestDispatchQueue.EnqueueAsync(
                            request,
                            registration.Completion,
                            timeoutSource.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                    when (!cancellationToken.IsCancellationRequested)
                {
                    _ = _requestRegistry.TryCompleteWithoutResponse(request.RequestId);
                    return BridgeDispatchResult.Timeout(request.RequestId);
                }
            }

            try
            {
                var completion = await registration.Completion
                    .WaitAsync(timeoutSource.Token)
                    .ConfigureAwait(false);

                return !completion.HasResponse || completion.Response is null
                    ? BridgeDispatchResult.Timeout(request.RequestId)
                    : BridgeDispatchResult.FromLive(completion.Response);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return BridgeDispatchResult.Timeout(request.RequestId);
            }
        }
        finally
        {
            _requestRegistry.Release(request.RequestId);
        }
    }

    private readonly IRequestRegistry _requestRegistry;
    private readonly IResponseCache _responseCache;
    private readonly UdpRequestDispatcher _requestDispatchQueue;
}
