using Models;

namespace Abstractions;

/// <summary>
/// Defines a registry for pending HTTP requests waiting on UDP responses.
/// </summary>
public interface IRequestRegistry
{
    /// <summary>
    /// Registers a request identifier or joins an existing pending registration.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>A registration that includes ownership and a completion task.</returns>
    PendingRequestRegistration Register(string requestId);

    /// <summary>
    /// Completes a pending request with a received UDP response.
    /// </summary>
    /// <param name="requestId">The request identifier to complete.</param>
    /// <param name="response">The received UDP response.</param>
    /// <returns>True when completion was applied; otherwise false.</returns>
    bool TryCompleteWithResponse(string requestId, CachedUdpResponse response);

    /// <summary>
    /// Completes a pending request when retries are exhausted without a response.
    /// </summary>
    /// <param name="requestId">The request identifier to complete.</param>
    /// <returns>True when completion was applied; otherwise false.</returns>
    bool TryCompleteWithoutResponse(string requestId);

    /// <summary>
    /// Releases one HTTP waiter for the specified request identifier.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    void Release(string requestId);
}
