namespace Models;

/// <summary>
/// Represents a parsed UDP request packet that is sent to the external endpoint.
/// </summary>
/// <param name="RequestId">The unique request identifier.</param>
/// <param name="Payload">The payload being sent through UDP.</param>
public sealed record UdpRequestPacket(string RequestId, string Payload);
