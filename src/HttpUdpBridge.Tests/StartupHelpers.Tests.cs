using Abstractions;

using FluentAssertions;

using HttpUdpBridge.Startup;

using Microsoft.AspNetCore.Builder;

namespace HttpUdpBridge.Tests;

public sealed class StartupHelpersTests
{
    [Fact(DisplayName = "CreateApplication registers bridge services")]
    [Trait("Category", "Unit")]
    public void CreateApplicationRegistersBridgeServices()
    {
        // Arrange & Act
        using var app = StartupHelpers.CreateApplication([]);

        // Assert
        _ = app.Services.GetService(typeof(IUdpRequestCoordinator))
            .Should().NotBeNull();
        _ = app.Services.GetService(typeof(IResponseCache))
            .Should().NotBeNull();
    }

    [Fact(DisplayName = "RunAppAsync throws when app is null")]
    [Trait("Category", "Unit")]
    public async Task RunAppAsyncWhenAppIsNullThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        WebApplication app = null!;

        // Act
        Func<Task> act = () => StartupHelpers.RunAppAsync(app);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
