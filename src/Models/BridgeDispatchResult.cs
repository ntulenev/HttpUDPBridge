namespace Models;

/// <summary>
/// Represents the result returned from the bridge coordinator to an HTTP endpoint.
/// </summary>
public sealed record BridgeDispatchResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BridgeDispatchResult"/> class.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="payload">The response payload, when available.</param>
    /// <param name="receivedAtUtc">The UTC timestamp of the UDP response.</param>
    /// <param name="isTimeout">Whether this result represents a timeout.</param>
    /// <param name="servedFromCache">Whether the response came from cache.</param>
    public BridgeDispatchResult(
        string requestId,
        string? payload,
        DateTimeOffset? receivedAtUtc,
        bool isTimeout,
        bool servedFromCache)
    {
        RequestId = requestId;
        Payload = payload;
        ReceivedAtUtc = receivedAtUtc;
        IsTimeout = isTimeout;
        ServedFromCache = servedFromCache;
    }

    /// <summary>
    /// The request identifier.
    /// </summary>
    public string RequestId { get; }

    /// <summary>
    /// The UDP response payload, if available.
    /// </summary>
    public string? Payload { get; }

    /// <summary>
    /// The UTC timestamp when the UDP response was received.
    /// </summary>
    public DateTimeOffset? ReceivedAtUtc { get; }

    /// <summary>
    /// Indicates whether the request timed out.
    /// </summary>
    public bool IsTimeout { get; }

    /// <summary>
    /// Indicates whether the response was served from cache.
    /// </summary>
    public bool ServedFromCache { get; }

    /// <summary>
    /// Creates a timeout result for the specified request identifier.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>A timeout bridge dispatch result.</returns>
    public static BridgeDispatchResult Timeout(string requestId) =>
        new(requestId, null, null, true, false);

    /// <summary>
    /// Creates a bridge result that was served from cache.
    /// </summary>
    /// <param name="response">The cached response.</param>
    /// <returns>A bridge dispatch result with cached data.</returns>
    public static BridgeDispatchResult FromCache(CachedUdpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new BridgeDispatchResult(
            response.RequestId,
            response.Payload,
            response.ReceivedAtUtc,
            false,
            true);
    }

    /// <summary>
    /// Creates a bridge result from a live UDP response.
    /// </summary>
    /// <param name="response">The live UDP response.</param>
    /// <returns>A bridge dispatch result with live data.</returns>
    public static BridgeDispatchResult FromLive(CachedUdpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new BridgeDispatchResult(
            response.RequestId,
            response.Payload,
            response.ReceivedAtUtc,
            false,
            false);
    }
}
