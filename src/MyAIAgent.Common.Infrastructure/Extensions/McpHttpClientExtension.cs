/**
 * MyAIAgent | Common · Infrastructure
 *
 * Configures factory-managed MCP clients with scoped credentials and explicit redirect and cookie boundaries.
 */

using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Infrastructure.Http;

namespace MyAIAgent.Common.Infrastructure.Extensions;

public static class McpHttpClientExtension
{
    public static IHttpClientBuilder AddMcpServiceAuthentication(
        this IHttpClientBuilder builder, McpServiceClientSettings settings) =>
        builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        }).AddHttpMessageHandler(() => new McpServiceKeyHandler(settings));
}
