using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using MyAIAgent.Common.Domain.Common;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Infrastructure.Helpers;

public interface IAgentHelper
{
    Task<IReadOnlyList<AITool>> GetAgentToolsAsync(AgentDefinition agent, CancellationToken cancellationToken);
}

public sealed class AgentHelper(ILogger<AgentHelper> logger)
    : IAgentHelper, ITransient
{
    public async Task<IReadOnlyList<AITool>> GetAgentToolsAsync(AgentDefinition agent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(agent.McpServerEndpoint))
            return [];

        try
        {
            // SseClientTransport creates its own internal HttpClient and does not support IHttpClientFactory.
            // Retry and timeout policies from IApiClient<T> cannot be applied here until the MCP SDK
            // exposes a factory-compatible transport. Track: upgrade when ModelContextProtocol adds support.
            await using var transport = new SseClientTransport(new SseClientTransportOptions { Endpoint = new Uri(agent.McpServerEndpoint) });
            await using var mcpClient = await McpClientFactory.CreateAsync(transport, cancellationToken: cancellationToken);
            var mcpTools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);

            var enabledNames = agent.AgentTools
                .Where(t => t.IsEnabled)
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var filtered = enabledNames.Count == 0
                ? mcpTools
                : mcpTools.Where(t => enabledNames.Contains(t.Name));

            return [.. filtered.Cast<AITool>()];
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Transient failure loading MCP tools from {Endpoint}", agent.McpServerEndpoint);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error loading MCP tools from {Endpoint}", agent.McpServerEndpoint);
        }

        return [];
    }
}
