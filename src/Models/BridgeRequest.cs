namespace Models;

/// <summary>
/// Represents an HTTP request that should be forwarded through the UDP bridge.
/// </summary>
/// <param name="RequestId">The unique request identifier.</param>
/// <param name="Payload">The payload that will be sent over UDP.</param>
public sealed record BridgeRequest(string RequestId, string Payload);
