using FluentAssertions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using UDPServerEmulator.Configuration;
using UDPServerEmulator.Startup;

namespace UDPServerEmulator.Tests;

public sealed class UdpServerUnitTests
{
    [Fact(DisplayName = "Constructor throws when options are null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenOptionsAreNullThrowsArgumentNullException()
    {
        // Arrange
        IOptions<UdpServerEmulatorOptions> options = null!;

        // Act
        Action act = () => _ = new UdpServer(options, NullLogger<UdpServer>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Constructor throws when logger is null")]
    [Trait("Category", "Unit")]
    public void ConstructorWhenLoggerIsNullThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(CreateValidOptions());
        ILogger<UdpServer> logger = null!;

        // Act
        Action act = () => _ = new UdpServer(options, logger);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "RunAppAsync throws when host is null")]
    [Trait("Category", "Unit")]
    public async Task RunAppAsyncWhenHostIsNullThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        IHost app = null!;

        // Act
        Func<Task> act = () => StartupHelpers.RunAppAsync(app);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "CreateApplication fails startup when options are invalid")]
    [Trait("Category", "Unit")]
    public async Task CreateApplicationFailsStartupWhenOptionsAreInvalidAsync()
    {
        // Arrange
        var args = new[]
        {
            "--UdpServerEmulator:ListenPort=0",
            "--UdpServerEmulator:MinDelayMilliseconds=0",
            "--UdpServerEmulator:MaxDelayMilliseconds=0",
            "--UdpServerEmulator:ResponsePrefix=test:"
        };
        using var app = StartupHelpers.CreateApplication(args);

        // Act
        Func<Task> act = () => app.StartAsync(CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<OptionsValidationException>();
    }

    private static UdpServerEmulatorOptions CreateValidOptions() =>
        new()
        {
            ListenPort = 8181,
            MinDelayMilliseconds = 0,
            MaxDelayMilliseconds = 0,
            ResponsePrefix = "test:"
        };
}
