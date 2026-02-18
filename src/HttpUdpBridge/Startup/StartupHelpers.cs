using Abstractions;

using Cache;

using Configuration;

using Services.BackgroundService;
using Services.Logic;

using Transport;

namespace HttpUdpBridge.Startup;

/// <summary>
/// Provides helper methods for application startup.
/// </summary>
internal static class StartupHelpers
{
    /// <summary>
    /// Builds and configures the <see cref="WebApplication"/> and its services.
    /// </summary>
    /// <param name="args">The application command-line arguments.</param>
    /// <returns>The configured <see cref="WebApplication"/> instance.</returns>
    public static WebApplication CreateApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureOptions(builder);
        RegisterServices(builder);

        return builder.Build();
    }

    /// <summary>
    /// Runs the configured application.
    /// </summary>
    /// <param name="app">The application instance.</param>
    /// <returns>A task that represents application execution.</returns>
    public static Task RunAppAsync(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.RunAsync();
    }

    private static void ConfigureOptions(WebApplicationBuilder builder)
    {
        _ = builder.Services
            .AddOptions<UdpEndpointOptions>()
            .Bind(builder.Configuration.GetSection("UdpEndpoint"))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.RemoteHost),
                "UdpEndpoint:RemoteHost must be configured.")
            .Validate(
                o => o.RemotePort is > 0 and <= 65535,
                "UdpEndpoint:RemotePort must be between 1 and 65535.")
            .Validate(
                o => o.LocalPort is >= 0 and <= 65535,
                "UdpEndpoint:LocalPort must be between 0 and 65535.")
            .ValidateOnStart();

        _ = builder.Services
            .AddOptions<UdpRetryOptions>()
            .Bind(builder.Configuration.GetSection("UdpRetry"))
            .Validate(
                o => o.AttemptTimeoutMilliseconds > 0,
                "UdpRetry:AttemptTimeoutMilliseconds must be positive.")
            .Validate(
                o => o.MaxAttempts > 0,
                "UdpRetry:MaxAttempts must be positive.")
            .Validate(
                o => o.DelayBetweenAttemptsMilliseconds >= 0,
                "UdpRetry:DelayBetweenAttemptsMilliseconds cannot be negative.")
            .Validate(
                o => o.QueueCapacity > 0,
                "UdpRetry:QueueCapacity must be positive.")
            .ValidateOnStart();

        _ = builder.Services
            .AddOptions<HttpBridgeOptions>()
            .Bind(builder.Configuration.GetSection("HttpBridge"))
            .Validate(
                o => o.RequestTimeoutMilliseconds > 0,
                "HttpBridge:RequestTimeoutMilliseconds must be positive.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.RequestIdHeaderName),
                "HttpBridge:RequestIdHeaderName must be configured.")
            .ValidateOnStart();

        _ = builder.Services
            .AddOptions<ResponseCacheOptions>()
            .Bind(builder.Configuration.GetSection("ResponseCache"))
            .Validate(
                o => o.TimeToLiveSeconds > 0,
                "ResponseCache:TimeToLiveSeconds must be positive.")
            .Validate(
                o => o.CleanupIntervalSeconds > 0,
                "ResponseCache:CleanupIntervalSeconds must be positive.")
            .ValidateOnStart();
    }

    private static void RegisterServices(WebApplicationBuilder builder)
    {
        _ = builder.Services.AddSingleton(TimeProvider.System);

        _ = builder.Services.AddSingleton<IRequestRegistry, RequestRegistry>();
        _ = builder.Services.AddSingleton<IResponseCache, MemoryResponseCache>();
        _ = builder.Services.AddSingleton<IUdpTransport, UdpTransport>();
        _ = builder.Services.AddSingleton<UdpRequestDispatcher>();
        _ = builder.Services.AddSingleton<IUdpRequestCoordinator, UdpRequestCoordinator>();

        _ = builder.Services.AddHostedService<UdpRequestDispatcherService>();
        _ = builder.Services.AddHostedService<UdpResponseListenerService>();
        _ = builder.Services.AddHostedService<ResponseCacheCleanupService>();
    }
}
