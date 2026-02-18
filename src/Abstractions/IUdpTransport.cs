using Models;

namespace Abstractions;

/// <summary>
/// Defines UDP transport operations for sending requests and receiving responses.
/// </summary>
public interface IUdpTransport
{
    /// <summary>
    /// Sends a UDP request packet to the configured endpoint.
    /// </summary>
    /// <param name="request">The request packet to send.</param>
    /// <param name="cancellationToken">A token that can cancel the asynchronous send operation.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    Task SendAsync(UdpRequestPacket request, CancellationToken cancellationToken);

    /// <summary>
    /// Receives the next valid UDP response packet.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the asynchronous receive operation.</param>
    /// <returns>A task that resolves to the next parsed UDP response packet.</returns>
    Task<UdpResponsePacket> ReceiveAsync(CancellationToken cancellationToken);
}
