using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Abstractions;

using Configuration;

using Microsoft.Extensions.Options;

using Models;

namespace Cache;

/// <summary>
/// Provides a thread-safe in-memory implementation of <see cref="IResponseCache"/>.
/// </summary>
public sealed class MemoryResponseCache : IResponseCache
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryResponseCache"/> class.
    /// </summary>
    /// <param name="options">Cache configuration options.</param>
    /// <param name="timeProvider">A time provider used for expiration checks.</param>
    public MemoryResponseCache(IOptions<ResponseCacheOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var cacheOptions = options.Value;
        _ttl = TimeSpan.FromSeconds(cacheOptions.TimeToLiveSeconds);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public bool TryGet(string requestId, [NotNullWhen(true)] out CachedUdpResponse? response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        if (!_entries.TryGetValue(requestId, out var entry))
        {
            response = null;
            return false;
        }

        var utcNow = _timeProvider.GetUtcNow();
        if (entry.ExpiresAtUtc <= utcNow)
        {
            _ = _entries.TryRemove(
                new KeyValuePair<string, CacheEntry>(requestId, entry));
            response = null;
            return false;
        }

        response = entry.Response;
        return true;
    }

    /// <inheritdoc />
    public void Store(CachedUdpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var utcNow = _timeProvider.GetUtcNow();
        var expiresAtUtc = utcNow + _ttl;
        _entries[response.RequestId] = new CacheEntry(response, expiresAtUtc);
    }

    /// <inheritdoc />
    public int RemoveExpired(DateTimeOffset utcNow)
    {
        var removed = 0;

        foreach (var entry in _entries)
        {
            if (entry.Value.ExpiresAtUtc > utcNow)
            {
                continue;
            }

            if (_entries.TryRemove(entry))
            {
                removed++;
            }
        }

        return removed;
    }

    private readonly ConcurrentDictionary<string, CacheEntry> _entries =
        new(StringComparer.Ordinal);

    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _ttl;

    private sealed record CacheEntry(
        CachedUdpResponse Response,
        DateTimeOffset ExpiresAtUtc);
}
