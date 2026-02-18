using Models;

namespace Abstractions;

/// <summary>
/// Defines the orchestration entry point for HTTP to UDP request dispatching.
/// </summary>
public interface IUdpRequestCoordinator
{
    /// <summary>
    /// Dispatches a bridge request through UDP and waits for completion up to the HTTP timeout.
    /// </summary>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="httpTimeout">The maximum HTTP wait time for this call.</param>
    /// <param name="cancellationToken">A token that can cancel the HTTP wait operation.</param>
    /// <returns>A task that resolves to the bridge dispatch result.</returns>
    Task<BridgeDispatchResult> DispatchAsync(
        BridgeRequest request,
        TimeSpan httpTimeout,
        CancellationToken cancellationToken);
}
