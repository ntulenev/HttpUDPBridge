namespace Configuration;

/// <summary>
/// Defines HTTP-facing bridge behavior and timeouts.
/// </summary>
public sealed class HttpBridgeOptions
{
    /// <summary>
    /// Maximum HTTP wait time in milliseconds for a UDP response.
    /// </summary>
    public required int RequestTimeoutMilliseconds { get; init; }

    /// <summary>
    /// Header name used for client-provided request identifiers.
    /// </summary>
    public required string RequestIdHeaderName { get; init; }
}
