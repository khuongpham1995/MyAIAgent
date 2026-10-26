/**
 * MyAIAgent | McpServer · Infrastructure
 *
 * Rejects extraction targets whose DNS answers contain private or special-purpose addresses.
 */

using System.Net;
using System.Net.Sockets;
using MyAIAgent.Common.Domain.Abstractions;
using MyAIAgent.Common.Infrastructure.Http;

namespace MyAIAgent.McpServer.Infrastructure.Services;

public interface IPublicWebPageHostPolicy
{
    Task<bool> IsAllowedAsync(string host, CancellationToken cancellationToken);
}

public sealed class PublicWebPageHostPolicy(Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null)
    : IPublicWebPageHostPolicy, ISingleton
{
    public async Task<bool> IsAllowedAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await (resolve ?? Dns.GetHostAddressesAsync)(host, cancellationToken);
            return addresses.Length > 0 && addresses.All(OutboundAddressPolicy.IsPublicAddress);
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
