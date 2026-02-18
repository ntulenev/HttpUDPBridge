using FluentAssertions;

using Models;

using Services.Logic;

namespace Services.Tests;

public sealed class RequestRegistryTests
{
    [Theory(DisplayName = "Register throws when request id is null or whitespace")]
    [Trait("Category", "Unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void RegisterWhenRequestIdIsInvalidThrowsArgumentException(string? requestId)
    {
        // Arrange
        var registry = new RequestRegistry();

        // Act
        Action act = () => _ = registry.Register(requestId!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Register returns owner for first request and joins for second")]
    [Trait("Category", "Unit")]
    public void RegisterReturnsOwnerForFirstRequestAndJoinsForSecond()
    {
        // Arrange
        var registry = new RequestRegistry();
        const string requestId = "request-1";

        // Act
        var first = registry.Register(requestId);
        var second = registry.Register(requestId);

        // Assert
        first.IsOwner.Should().BeTrue();
        second.IsOwner.Should().BeFalse();
        second.Completion.Should().BeSameAs(first.Completion);

        registry.Release(requestId);
        registry.Release(requestId);
    }

    [Fact(DisplayName = "TryCompleteWithResponse returns false when request is missing")]
    [Trait("Category", "Unit")]
    public void TryCompleteWithResponseReturnsFalseWhenRequestIsMissing()
    {
        // Arrange
        var registry = new RequestRegistry();
        var response = new CachedUdpResponse(
            "request-1",
            "payload-1",
            new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero));

        // Act
        var completed = registry.TryCompleteWithResponse("missing", response);

        // Assert
        completed.Should().BeFalse();
    }

    [Fact(DisplayName = "TryCompleteWithResponse completes pending request")]
    [Trait("Category", "Unit")]
    public async Task TryCompleteWithResponseCompletesPendingRequestAsync()
    {
        // Arrange
        var registry = new RequestRegistry();
        const string requestId = "request-1";
        var registration = registry.Register(requestId);
        var response = new CachedUdpResponse(
            requestId,
            "payload-1",
            new DateTimeOffset(2026, 2, 18, 0, 0, 0, TimeSpan.Zero));

        // Act
        var completed = registry.TryCompleteWithResponse(requestId, response);
        var result = await registration.Completion.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        completed.Should().BeTrue();
        result.HasResponse.Should().BeTrue();
        result.Response.Should().Be(response);

        registry.Release(requestId);
    }

    [Fact(DisplayName = "TryCompleteWithoutResponse completes pending request")]
    [Trait("Category", "Unit")]
    public async Task TryCompleteWithoutResponseCompletesPendingRequestAsync()
    {
        // Arrange
        var registry = new RequestRegistry();
        const string requestId = "request-1";
        var registration = registry.Register(requestId);

        // Act
        var completed = registry.TryCompleteWithoutResponse(requestId);
        var result = await registration.Completion.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        completed.Should().BeTrue();
        result.Should().Be(PendingUdpRequestResult.NoResponse);

        registry.Release(requestId);
    }

    [Fact(DisplayName = "Release after completion removes request state")]
    [Trait("Category", "Unit")]
    public void ReleaseAfterCompletionRemovesRequestState()
    {
        // Arrange
        var registry = new RequestRegistry();
        const string requestId = "request-1";

        var first = registry.Register(requestId);
        _ = registry.Register(requestId);
        _ = registry.TryCompleteWithoutResponse(requestId);

        // Act
        registry.Release(requestId);
        registry.Release(requestId);
        var next = registry.Register(requestId);

        // Assert
        next.IsOwner.Should().BeTrue();
        next.Completion.Should().NotBeSameAs(first.Completion);
        next.Completion.IsCompleted.Should().BeFalse();

        _ = registry.TryCompleteWithoutResponse(requestId);
        registry.Release(requestId);
    }
}
