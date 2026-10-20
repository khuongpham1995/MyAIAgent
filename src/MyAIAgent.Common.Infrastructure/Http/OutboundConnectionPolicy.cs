/**
 * MyAIAgent | Common · Infrastructure
 *
 * Resolves once per connection and connects only to the validated address set, retaining the request hostname for TLS.
 */

using System.Net;
using System.Net.Sockets;

namespace MyAIAgent.Common.Infrastructure.Http;

public sealed class OutboundConnectionPolicy(
    Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null,
    Func<IPAddress[], int, CancellationToken, ValueTask<Stream>>? connect = null)
{
    public async ValueTask<Stream> ConnectAsync(
        DnsEndPoint endpoint, Func<IPAddress, bool> isAllowed, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var addresses = await (resolve ?? Dns.GetHostAddressesAsync)(endpoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !isAllowed(address)))
            throw new OutboundDestinationBlockedException("dns-address");
        return await (connect ?? ConnectSocketAsync)(addresses, endpoint.Port, cancellationToken);
    }

    private static async ValueTask<Stream> ConnectSocketAsync(IPAddress[] addresses, int port, CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(addresses, port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
