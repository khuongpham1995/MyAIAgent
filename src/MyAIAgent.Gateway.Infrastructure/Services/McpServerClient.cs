/**
 * MyAIAgent | Gateway · Infrastructure
 *
 * Discovers tools through an MCP HTTP session and returns their names and descriptions to Gateway handlers.
 */

using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Domain.Constants;
using MyAIAgent.Gateway.Application.Common.Interfaces;
using MyAIAgent.Gateway.Application.Admin;

namespace MyAIAgent.Gateway.Infrastructure.Services;

public sealed class McpServerClient(
    HttpClient httpClient,
    ILogger<McpServerClient> logger) : IMcpServerClient
{
    public async Task<ResponseModel<IReadOnlyList<ToolInfo>>> ListToolsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp")
            }, httpClient);
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);

            return ResponseModel<IReadOnlyList<ToolInfo>>.Succeed(
                tools.Select(tool => new ToolInfo(tool.Name, tool.Description ?? string.Empty)).ToList());
        }
        catch (Exception ex) when (ex is HttpRequestException or McpException)
        {
            logger.LogError(ex, "MCP Server tool listing failed");
            return ResponseModel<IReadOnlyList<ToolInfo>>.Fail(message: GatewayConstant.Errors.ToolListFailed);
        }
    }
}
