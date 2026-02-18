namespace Models;

/// <summary>
/// Represents the completion state of a pending UDP request.
/// </summary>
public sealed record PendingUdpRequestResult
{
    /// <summary>
    /// Gets a result that indicates no UDP response was received before retries were exhausted.
    /// </summary>
    public static PendingUdpRequestResult NoResponse { get; } = new(false, null);

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingUdpRequestResult"/> class.
    /// </summary>
    /// <param name="hasResponse">Whether a UDP response is available.</param>
    /// <param name="response">The UDP response, if available.</param>
    public PendingUdpRequestResult(bool hasResponse, CachedUdpResponse? response)
    {
        HasResponse = hasResponse;
        Response = response;
    }

    /// <summary>
    /// Indicates whether a UDP response was received.
    /// </summary>
    public bool HasResponse { get; }

    /// <summary>
    /// Gets the UDP response when <see cref="HasResponse"/> is true.
    /// </summary>
    public CachedUdpResponse? Response { get; }

    /// <summary>
    /// Creates a completion result that contains a UDP response.
    /// </summary>
    /// <param name="response">The received UDP response.</param>
    /// <returns>A completed result containing the response payload.</returns>
    public static PendingUdpRequestResult WithResponse(CachedUdpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new PendingUdpRequestResult(true, response);
    }
}
