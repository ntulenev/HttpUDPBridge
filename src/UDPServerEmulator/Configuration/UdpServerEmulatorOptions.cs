namespace UDPServerEmulator.Configuration;

/// <summary>
/// Defines configuration settings for the UDP server emulator.
/// </summary>
internal sealed class UdpServerEmulatorOptions
{
    /// <summary>
    /// The UDP port that the emulator listens on.
    /// </summary>
    public required int ListenPort { get; init; }

    /// <summary>
    /// The minimum random delay in milliseconds before sending a response.
    /// </summary>
    public required int MinDelayMilliseconds { get; init; }

    /// <summary>
    /// The maximum random delay in milliseconds before sending a response.
    /// </summary>
    public required int MaxDelayMilliseconds { get; init; }

    /// <summary>
    /// The prefix applied to emulated response payloads.
    /// </summary>
    public required string ResponsePrefix { get; init; }
}
