namespace Models;

/// <summary>
/// Represents a registration for a pending HTTP request waiting on UDP completion.
/// </summary>
/// <param name="RequestId">The request identifier associated with this registration.</param>
/// <param name="Completion">A task that completes when the UDP workflow finishes.</param>
/// <param name="IsOwner">True when this registration created the pending workflow.</param>
public sealed record PendingRequestRegistration(
    string RequestId,
    Task<PendingUdpRequestResult> Completion,
    bool IsOwner);
