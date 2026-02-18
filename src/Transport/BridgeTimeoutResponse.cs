namespace Transport;

/// <summary>
/// Represents a timeout response payload for bridge requests.
/// </summary>
/// <param name="RequestId">The request identifier.</param>
/// <param name="Error">A timeout error message.</param>
public sealed record BridgeTimeoutResponse(string RequestId, string Error);
