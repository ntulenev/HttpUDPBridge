namespace Configuration;

/// <summary>
/// Defines UDP endpoint settings used by the bridge transport.
/// </summary>
public sealed class UdpEndpointOptions
{
    /// <summary>
    /// The remote host name or IP address for the UDP receiver.
    /// </summary>
    public required string RemoteHost { get; init; }

    /// <summary>
    /// The remote UDP port of the receiver.
    /// </summary>
    public required int RemotePort { get; init; }

    /// <summary>
    /// Optional local port used for receiving UDP responses. Use 0 for ephemeral port.
    /// </summary>
    public int LocalPort { get; init; }
}
