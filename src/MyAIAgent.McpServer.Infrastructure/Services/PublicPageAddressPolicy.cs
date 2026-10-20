/**
 * MyAIAgent | McpServer · Infrastructure
 *
 * Validates public HTTP destinations and resolved addresses before outbound page-fetch connections.
 */

using System.Net;
using MyAIAgent.Common.Infrastructure.Http;

namespace MyAIAgent.McpServer.Infrastructure.Services;

public static class PublicPageAddressPolicy
{
    public static bool IsAllowedUri(Uri uri) => OutboundAddressPolicy.IsPublicUri(uri) && uri.IsDefaultPort;

    public static bool IsPublicAddress(IPAddress address) => OutboundAddressPolicy.IsPublicAddress(address);
}
