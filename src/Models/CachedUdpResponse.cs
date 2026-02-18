namespace Models;

/// <summary>
/// Represents a cached UDP response payload and metadata.
/// </summary>
/// <param name="RequestId">The unique request identifier.</param>
/// <param name="Payload">The response payload.</param>
/// <param name="ReceivedAtUtc">The UTC timestamp when the response was received.</param>
public sealed record CachedUdpResponse(
    string RequestId,
    string Payload,
    DateTimeOffset ReceivedAtUtc);
