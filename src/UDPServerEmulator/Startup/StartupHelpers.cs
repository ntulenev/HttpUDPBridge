using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using UDPServerEmulator.Configuration;

namespace UDPServerEmulator.Startup;

/// <summary>
/// Provides helper methods for emulator startup.
/// </summary>
internal static class StartupHelpers
{
    /// <summary>
    /// Builds and configures the emulator host and services.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The configured host instance.</returns>
    public static IHost CreateApplication(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        ConfigureOptions(builder);
        RegisterServices(builder);

        return builder.Build();
    }

    /// <summary>
    /// Runs the emulator host.
    /// </summary>
    /// <param name="app">The configured host.</param>
    /// <returns>A task that represents the host run operation.</returns>
    public static Task RunAppAsync(IHost app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.RunAsync();
    }

    private static void ConfigureOptions(HostApplicationBuilder builder)
    {
        _ = builder.Services
            .AddOptions<UdpServerEmulatorOptions>()
            .Bind(builder.Configuration.GetSection("UdpServerEmulator"))
            .Validate(
                o => o.ListenPort is > 0 and <= 65535,
                "UdpServerEmulator:ListenPort must be between 1 and 65535.")
            .Validate(
                o => o.MinDelayMilliseconds >= 0,
                "UdpServerEmulator:MinDelayMilliseconds cannot be negative.")
            .Validate(
                o => o.MaxDelayMilliseconds >= o.MinDelayMilliseconds,
                "UdpServerEmulator:MaxDelayMilliseconds must be >= MinDelayMilliseconds.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ResponsePrefix),
                "UdpServerEmulator:ResponsePrefix must be configured.")
            .ValidateOnStart();
    }

    private static void RegisterServices(HostApplicationBuilder builder)
    {
        _ = builder.Services.AddHostedService<UdpServer>();
    }
}
