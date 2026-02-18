namespace Configuration;

/// <summary>
/// Defines retry behavior for UDP request attempts.
/// </summary>
public sealed class UdpRetryOptions
{
    /// <summary>
    /// Timeout in milliseconds for each UDP attempt.
    /// </summary>
    public required int AttemptTimeoutMilliseconds { get; init; }

    /// <summary>
    /// Maximum number of UDP retry attempts.
    /// </summary>
    public required int MaxAttempts { get; init; }

    /// <summary>
    /// Optional delay in milliseconds between retry attempts.
    /// </summary>
    public int DelayBetweenAttemptsMilliseconds { get; init; }

    /// <summary>
    /// Maximum number of pending requests buffered in the dispatch queue.
    /// </summary>
    public required int QueueCapacity { get; init; }
}
