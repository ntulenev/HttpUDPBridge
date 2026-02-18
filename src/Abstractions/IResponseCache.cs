using System.Diagnostics.CodeAnalysis;

using Models;

namespace Abstractions;

/// <summary>
/// Defines a cache for UDP responses keyed by request identifier.
/// </summary>
public interface IResponseCache
{
    /// <summary>
    /// Attempts to fetch a cached response for the specified request identifier.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="response">The cached response when available.</param>
    /// <returns>True when a non-expired cached response exists.</returns>
    bool TryGet(string requestId, [NotNullWhen(true)] out CachedUdpResponse? response);

    /// <summary>
    /// Stores a response in cache for the configured cache TTL.
    /// </summary>
    /// <param name="response">The response to cache.</param>
    void Store(CachedUdpResponse response);

    /// <summary>
    /// Removes expired cache entries.
    /// </summary>
    /// <param name="utcNow">The current UTC timestamp used for expiration checks.</param>
    /// <returns>The number of removed entries.</returns>
    int RemoveExpired(DateTimeOffset utcNow);
}
