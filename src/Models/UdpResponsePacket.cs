namespace Models;

/// <summary>
/// Represents a parsed UDP response packet received from the external endpoint.
/// </summary>
/// <param name="RequestId">The unique request identifier.</param>
/// <param name="Payload">The payload received through UDP.</param>
/// <param name="ReceivedAtUtc">The UTC timestamp when the packet was received.</param>
public sealed record UdpResponsePacket(
    string RequestId,
    string Payload,
    DateTimeOffset ReceivedAtUtc);
