using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
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
            // Auto-detection supports Streamable HTTP and existing SSE endpoints without replaying tool calls.
            transport = new HttpClientTransport(
                new HttpClientTransportOptions { Endpoint = new Uri(agent.McpServerEndpoint) }, httpClient);
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
