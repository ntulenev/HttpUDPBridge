namespace Transport;

/// <summary>
/// Represents an HTTP bridge request payload.
/// </summary>
/// <param name="Payload">The payload that will be sent to UDP.</param>
public sealed record BridgeHttpRequest(string Payload);
