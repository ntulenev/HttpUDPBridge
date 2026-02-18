using FluentAssertions;

using Configuration;

using Microsoft.Extensions.Options;

using Models;

namespace Cache.Tests;

public sealed class MemoryResponseCacheTests
{
    [Fact(DisplayName = "Constructor throws when options is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsIsNullThrowsArgumentNullException()
    {
        // Arrange
        IOptions<ResponseCacheOptions> options = null!;
        var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);

        // Act
        Action act = () => _ = new MemoryResponseCache(options, timeProvider);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when time provider is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenTimeProviderIsNullThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateOptions(timeToLiveSeconds: 5);
        TimeProvider timeProvider = null!;

        // Act
        Action act = () => _ = new MemoryResponseCache(options, timeProvider);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "TryGet throws when request id is null")]
    [Trait("Category", "Unit")]
    public void TryGetWhenRequestIdIsNullThrowsArgumentNullException()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(utcNow);
        var cache = CreateCache(timeProvider, timeToLiveSeconds: 5);
        string requestId = null!;

        // Act
        Action act = () => _ = cache.TryGet(requestId, out _);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "TryGet returns false when response is missing")]
    [Trait("Category", "Unit")]
    public void TryGetReturnsFalseWhenResponseIsMissing()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(utcNow);
        var cache = CreateCache(timeProvider, timeToLiveSeconds: 5);

        // Act
        var found = cache.TryGet("missing", out var response);

        // Assert
        found.Should().BeFalse();
        response.Should().BeNull();
    }

    [Fact(DisplayName = "Store throws when response is null")]
    [Trait("Category", "Unit")]
    public void StoreWhenResponseIsNullThrowsArgumentNullException()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(utcNow);
        var cache = CreateCache(timeProvider, timeToLiveSeconds: 5);
        CachedUdpResponse response = null!;

        // Act
        Action act = () => cache.Store(response);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Store and TryGet return cached response before expiration")]
    [Trait("Category", "Unit")]
    public void StoreAndTryGetReturnCachedResponseBeforeExpiration()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(utcNow);
        var cache = CreateCache(timeProvider, timeToLiveSeconds: 5);
        var requestId = "request-1";
        var storedResponse = new CachedUdpResponse(
            requestId,
            "payload-1",
            utcNow);

        // Act
        cache.Store(storedResponse);
        var found = cache.TryGet(requestId, out var response);

        // Assert
        found.Should().BeTrue();
        response.Should().Be(storedResponse);
    }

    [Fact(DisplayName = "TryGet returns false and removes entry when response is expired")]
    [Trait("Category", "Unit")]
    public void TryGetReturnsFalseAndRemovesEntryWhenResponseIsExpired()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(utcNow);
        var cache = CreateCache(timeProvider, timeToLiveSeconds: 5);
        var requestId = "request-1";
        var storedResponse = new CachedUdpResponse(
            requestId,
            "payload-1",
            utcNow);

        cache.Store(storedResponse);
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        // Act
        var found = cache.TryGet(requestId, out var response);
        var removed = cache.RemoveExpired(timeProvider.GetUtcNow());

        // Assert
        found.Should().BeFalse();
        response.Should().BeNull();
        removed.Should().Be(0);
    }

    [Fact(DisplayName = "RemoveExpired removes only expired entries")]
    [Trait("Category", "Unit")]
    public void RemoveExpiredRemovesOnlyExpiredEntries()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(utcNow);
        var cache = CreateCache(timeProvider, timeToLiveSeconds: 5);
        var first = new CachedUdpResponse("request-1", "payload-1", utcNow);

        cache.Store(first);
        timeProvider.Advance(TimeSpan.FromSeconds(3));

        var second = new CachedUdpResponse("request-2", "payload-2", timeProvider.GetUtcNow());
        cache.Store(second);
        timeProvider.Advance(TimeSpan.FromSeconds(3));

        // Act
        var removed = cache.RemoveExpired(timeProvider.GetUtcNow());
        var firstFound = cache.TryGet(first.RequestId, out _);
        var secondFound = cache.TryGet(second.RequestId, out var secondResponse);

        // Assert
        removed.Should().Be(1);
        firstFound.Should().BeFalse();
        secondFound.Should().BeTrue();
        secondResponse.Should().Be(second);
    }

    [Fact(DisplayName = "Store replaces cached response with the same request id")]
    [Trait("Category", "Unit")]
    public void StoreReplacesCachedResponseWithTheSameRequestId()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(utcNow);
        var cache = CreateCache(timeProvider, timeToLiveSeconds: 5);
        var requestId = "request-1";
        var firstResponse = new CachedUdpResponse(requestId, "payload-1", utcNow);

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var secondResponse = new CachedUdpResponse(
            requestId,
            "payload-2",
            timeProvider.GetUtcNow());

        // Act
        cache.Store(firstResponse);
        cache.Store(secondResponse);
        var found = cache.TryGet(requestId, out var response);

        // Assert
        found.Should().BeTrue();
        response.Should().Be(secondResponse);
    }

    private static IOptions<ResponseCacheOptions> CreateOptions(int timeToLiveSeconds) =>
        Options.Create(new ResponseCacheOptions
        {
            TimeToLiveSeconds = timeToLiveSeconds,
            CleanupIntervalSeconds = 1
        });

    private static MemoryResponseCache CreateCache(
        TimeProvider timeProvider,
        int timeToLiveSeconds) =>
        new(CreateOptions(timeToLiveSeconds), timeProvider);

    private sealed class MutableTimeProvider : TimeProvider
    {
#pragma warning disable IDE0021 // Use block body for constructor
        public MutableTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
#pragma warning restore IDE0021 // Use block body for constructor

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;

        private DateTimeOffset _utcNow;
    }
}
