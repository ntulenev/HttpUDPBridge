namespace Transport;

/// <summary>
/// Represents a successful HTTP bridge response payload.
/// </summary>
/// <param name="RequestId">The request identifier.</param>
/// <param name="Payload">The UDP response payload.</param>
/// <param name="FromCache">Whether the response was served from cache.</param>
/// <param name="ReceivedAtUtc">The UTC timestamp of response arrival.</param>
public sealed record BridgeHttpResponse(
    string RequestId,
    string Payload,
    bool FromCache,
    DateTimeOffset ReceivedAtUtc);
