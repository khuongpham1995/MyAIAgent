/**
 * MyAIAgent | McpServer · Infrastructure
 *
 * Validates public HTTP destinations and resolved addresses before outbound page-fetch connections.
 */

using System.Net;
using System.Net.Sockets;

namespace MyAIAgent.McpServer.Infrastructure.Services;

public static class PublicPageAddressPolicy
{
    public static bool IsAllowedUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            return false;

        return !IPAddress.TryParse(uri.Host, out var address) || IsPublicAddress(address);
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] is >= 1 and <= 223 &&
                bytes[0] != 10 &&
                bytes[0] != 127 &&
                !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) &&
                !(bytes[0] == 169 && bytes[1] == 254) &&
                !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
                !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) &&
                !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) &&
                !(bytes[0] == 192 && bytes[1] == 168) &&
                !(bytes[0] == 198 && bytes[1] is 18 or 19) &&
                !(bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) &&
                !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6 &&
            bytes[0] is >= 0x20 and <= 0x3f &&
            !address.IsIPv6LinkLocal &&
            !address.IsIPv6SiteLocal &&
            !address.IsIPv6Multicast &&
            !IPAddress.IsLoopback(address);
    }

    public static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var endpoint = context.DnsEndPoint;
        var addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            throw new HttpRequestException("Page destination is not public.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(addresses, endpoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
