using Microsoft.Extensions.Hosting;

namespace Services;

/// <summary>
/// Hosts <see cref="UdpRequestDispatcher"/> as a background worker.
/// </summary>
public sealed class UdpRequestDispatcherService : BackgroundService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UdpRequestDispatcherService"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to run.</param>
    public UdpRequestDispatcherService(UdpRequestDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _dispatcher.RunAsync(stoppingToken);

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _dispatcher.Complete();
        return base.StopAsync(cancellationToken);
    }

    private readonly UdpRequestDispatcher _dispatcher;
}
