namespace UDPServerEmulator.Serialization;

/// <summary>
/// Represents the UDP wire-format message exchanged with the bridge.
/// </summary>
/// <param name="RequestId">The unique request identifier.</param>
/// <param name="Payload">The request or response payload.</param>
internal sealed record UdpWireMessage(string RequestId, string? Payload);
