namespace Configuration;

/// <summary>
/// Defines response cache retention and maintenance settings.
/// </summary>
public sealed class ResponseCacheOptions
{
    /// <summary>
    /// Cache entry time-to-live in seconds.
    /// </summary>
    public required int TimeToLiveSeconds { get; init; }

    /// <summary>
    /// Expired cache cleanup interval in seconds.
    /// </summary>
    public required int CleanupIntervalSeconds { get; init; }
}
