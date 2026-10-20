/**
 * MyAIAgent | Orchestrator · Infrastructure
 *
 * Discovers an agent's MCP tools and retains transport resources until the chat turn completes.
 */

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MyAIAgent.Common.Infrastructure.Http;
using System.Net;
using ModelContextProtocol.Client;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Infrastructure.Helpers;

public interface IAgentHelper
{
    Task<AgentToolSet> GetAgentToolsAsync(AgentDefinition agent, CancellationToken cancellationToken);
}

// MCP tools invoke through their client, so the connection must outlive tool discovery until the chat turn ends.
public sealed class AgentToolSet(IReadOnlyList<AITool> tools, params IAsyncDisposable[] resources) : IAsyncDisposable
{
    public static AgentToolSet Empty { get; } = new([]);

    public IReadOnlyList<AITool> Tools { get; } = tools;

    public async ValueTask DisposeAsync()
    {
        foreach (var resource in resources.Reverse())
            await resource.DisposeAsync();
    }
}

public sealed class AgentHelper(HttpClient httpClient, ILogger<AgentHelper> logger)
    : IAgentHelper
{
    public async Task<AgentToolSet> GetAgentToolsAsync(AgentDefinition agent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(agent.McpServerEndpoint))
            return AgentToolSet.Empty;

        HttpClientTransport? transport = null;
        McpClient? mcpClient = null;

        try
        {
            var endpoint = new Uri(agent.McpServerEndpoint);
            // The registered base address is trusted; other agent endpoints retain anonymous transport compatibility.
            var isTrusted = httpClient.BaseAddress is { } origin
                && McpServiceClientSettings.IsTrustedEndpoint(origin, endpoint);
            transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = endpoint,
                    TransportMode = isTrusted ? HttpTransportMode.StreamableHttp : HttpTransportMode.AutoDetect
                }, httpClient);
            mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            var mcpTools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);

            var enabledNames = agent.AgentTools
                .Where(t => t.IsEnabled)
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var filtered = enabledNames.Count == 0
                ? mcpTools
                : mcpTools.Where(t => enabledNames.Contains(t.Name));

            return new AgentToolSet([.. filtered.Cast<AITool>()], transport, mcpClient);
        }
        catch (OperationCanceledException)
        {
            await DisposeQuietlyAsync(mcpClient);
            await DisposeQuietlyAsync(transport);
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            await DisposeQuietlyAsync(mcpClient);
            await DisposeQuietlyAsync(transport);
            // Chat and streaming handlers already turn HTTP failures into safe errors without committing a turn.
            throw new HttpRequestException("MCP service authentication failed.", null, ex.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Transient failure loading MCP tools from {Endpoint}", agent.McpServerEndpoint);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error loading MCP tools from {Endpoint}", agent.McpServerEndpoint);
        }

        await DisposeQuietlyAsync(mcpClient);
        await DisposeQuietlyAsync(transport);
        return AgentToolSet.Empty;
    }

    private static async ValueTask DisposeQuietlyAsync(IAsyncDisposable? resource)
    {
        if (resource is not null)
            await resource.DisposeAsync();
    }
}
