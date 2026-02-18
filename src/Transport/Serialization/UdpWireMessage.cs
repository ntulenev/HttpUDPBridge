namespace Transport.Serialization;

/// <summary>
/// Represents the internal UDP wire-format payload used by this bridge.
/// </summary>
/// <param name="RequestId">The request identifier.</param>
/// <param name="Payload">The payload string.</param>
internal sealed record UdpWireMessage(string RequestId, string? Payload);
