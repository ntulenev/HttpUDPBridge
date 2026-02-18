using FluentAssertions;

using Abstractions;

using Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using Services.BackgroundService;

namespace Services.Tests;

public sealed class ResponseCacheCleanupServiceTests
{
    [Fact(DisplayName = "Constructor throws when response cache is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenResponseCacheIsNullThrowsArgumentNullException()
    {
        // Arrange
        IResponseCache responseCache = null!;
        var options = CreateOptions();
        var timeProvider = TimeProvider.System;
        var logger = NullLogger<ResponseCacheCleanupService>.Instance;

        // Act
        Action act = () => _ = new ResponseCacheCleanupService(
            responseCache,
            options,
            timeProvider,
            logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when options are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsAreNullThrowsArgumentNullException()
    {
        // Arrange
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        IOptions<ResponseCacheOptions> options = null!;
        var timeProvider = TimeProvider.System;
        var logger = NullLogger<ResponseCacheCleanupService>.Instance;

        // Act
        Action act = () => _ = new ResponseCacheCleanupService(
            responseCacheMock.Object,
            options,
            timeProvider,
            logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when time provider is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenTimeProviderIsNullThrowsArgumentNullException()
    {
        // Arrange
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var options = CreateOptions();
        TimeProvider timeProvider = null!;
        var logger = NullLogger<ResponseCacheCleanupService>.Instance;

        // Act
        Action act = () => _ = new ResponseCacheCleanupService(
            responseCacheMock.Object,
            options,
            timeProvider,
            logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when logger is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenLoggerIsNullThrowsArgumentNullException()
    {
        // Arrange
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var options = CreateOptions();
        var timeProvider = TimeProvider.System;
        ILogger<ResponseCacheCleanupService> logger = null!;

        // Act
        Action act = () => _ = new ResponseCacheCleanupService(
            responseCacheMock.Object,
            options,
            timeProvider,
            logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Service periodically removes expired cache entries")]
    [Trait("Category", "Unit")]
    public async Task ServicePeriodicallyRemovesExpiredCacheEntriesAsync()
    {
        // Arrange
        var responseCacheMock = new Mock<IResponseCache>(MockBehavior.Strict);
        var options = CreateOptions(cleanupIntervalSeconds: 1);
        var cleanupSource = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var removeExpiredCallCount = 0;

        responseCacheMock
            .Setup(cache => cache.RemoveExpired(It.IsAny<DateTimeOffset>()))
            .Callback(() =>
            {
                removeExpiredCallCount++;
                _ = cleanupSource.TrySetResult(true);
            })
            .Returns(0);

        using var service = new ResponseCacheCleanupService(
            responseCacheMock.Object,
            options,
            TimeProvider.System,
            NullLogger<ResponseCacheCleanupService>.Instance);

        // Act
        await service.StartAsync(CancellationToken.None);
        _ = await cleanupSource.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await service.StopAsync(CancellationToken.None);

        // Assert
        removeExpiredCallCount.Should().BeGreaterThanOrEqualTo(1);
        responseCacheMock.Verify(cache => cache.RemoveExpired(
            It.IsAny<DateTimeOffset>()), Times.AtLeastOnce);
        responseCacheMock.VerifyNoOtherCalls();
    }

    private static IOptions<ResponseCacheOptions> CreateOptions(
        int timeToLiveSeconds = 10,
        int cleanupIntervalSeconds = 1) =>
        Options.Create(new ResponseCacheOptions
        {
            TimeToLiveSeconds = timeToLiveSeconds,
            CleanupIntervalSeconds = cleanupIntervalSeconds
        });
}
