using Abstractions;

using Configuration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Services;

/// <summary>
/// Periodically removes expired entries from the in-memory response cache.
/// </summary>
public sealed class ResponseCacheCleanupService : BackgroundService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseCacheCleanupService"/> class.
    /// </summary>
    /// <param name="responseCache">The cache to clean up.</param>
    /// <param name="options">Cleanup and TTL configuration options.</param>
    /// <param name="timeProvider">A time provider for deterministic time operations.</param>
    /// <param name="logger">The logger instance.</param>
    public ResponseCacheCleanupService(
        IResponseCache responseCache,
        IOptions<ResponseCacheOptions> options,
        TimeProvider timeProvider,
        ILogger<ResponseCacheCleanupService> logger)
    {
        ArgumentNullException.ThrowIfNull(responseCache);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _responseCache = responseCache;
        _cleanupInterval = TimeSpan.FromSeconds(options.Value.CleanupIntervalSeconds);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_cleanupInterval, _timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var removed = _responseCache.RemoveExpired(_timeProvider.GetUtcNow());
            if (removed > 0 && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Removed {Count} expired response cache entries.", removed);
            }
        }
    }

    private readonly IResponseCache _responseCache;
    private readonly TimeSpan _cleanupInterval;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ResponseCacheCleanupService> _logger;
}
