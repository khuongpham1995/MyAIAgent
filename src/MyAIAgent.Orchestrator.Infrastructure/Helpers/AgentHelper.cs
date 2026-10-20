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

public sealed class AgentHelper(HttpClient httpClient, ILogger<AgentHelper> logger,
    McpServiceClientSettings settings, IHttpClientFactory clientFactory)
    : IAgentHelper
{
    public const string ExternalClientName = "external-mcp";

    public async Task<AgentToolSet> GetAgentToolsAsync(AgentDefinition agent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(agent.McpServerEndpoint))
            return AgentToolSet.Empty;

        HttpClientTransport? transport = null;
        McpClient? mcpClient = null;
        HttpClient? externalClient = null;

        try
        {
            if (!settings.IsAllowedEndpoint(agent.McpServerEndpoint))
                throw new OutboundDestinationBlockedException("agent-endpoint");
            var endpoint = new Uri(agent.McpServerEndpoint);
            var isTrusted = settings.IsTrustedEndpoint(endpoint);
            if (!isTrusted)
                externalClient = clientFactory.CreateClient(ExternalClientName);
            transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = endpoint,
                    TransportMode = isTrusted ? HttpTransportMode.StreamableHttp : HttpTransportMode.AutoDetect
                }, externalClient ?? httpClient);
            mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            var mcpTools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);

            var enabledNames = agent.AgentTools
                .Where(t => t.IsEnabled)
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var filtered = enabledNames.Count == 0
                ? mcpTools
                : mcpTools.Where(t => enabledNames.Contains(t.Name));

            return externalClient is null
                ? new AgentToolSet([.. filtered.Cast<AITool>()], transport, mcpClient)
                : new AgentToolSet([.. filtered.Cast<AITool>()], new AsyncClientOwner(externalClient), transport, mcpClient);
        }
        catch (OperationCanceledException)
        {
            await DisposeQuietlyAsync(mcpClient);
            await DisposeQuietlyAsync(transport);
            externalClient?.Dispose();
            throw;
        }
        catch (Exception ex) when (OutboundDestinationBlockedException.Find(ex) is not null)
        {
            await DisposeQuietlyAsync(mcpClient);
            await DisposeQuietlyAsync(transport);
            externalClient?.Dispose();
            var reason = OutboundDestinationBlockedException.Find(ex)!.Reason;
            logger.LogWarning("MCP destination rejected for agent {AgentId}: {Reason}", agent.Id, reason);
            throw new OutboundDestinationBlockedException(reason);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            await DisposeQuietlyAsync(mcpClient);
            await DisposeQuietlyAsync(transport);
            externalClient?.Dispose();
            // Chat and streaming handlers already turn HTTP failures into safe errors without committing a turn.
            throw new HttpRequestException("MCP service authentication failed.", null, ex.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("MCP discovery failed for agent {AgentId}: {FailureType}", agent.Id, ex.GetType().Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError("MCP discovery failed for agent {AgentId}: {FailureType}", agent.Id, ex.GetType().Name);
        }

        await DisposeQuietlyAsync(mcpClient);
        await DisposeQuietlyAsync(transport);
        externalClient?.Dispose();
        return AgentToolSet.Empty;
    }

    private sealed class AsyncClientOwner(HttpClient client) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            client.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static async ValueTask DisposeQuietlyAsync(IAsyncDisposable? resource)
    {
        if (resource is not null)
            await resource.DisposeAsync();
    }
}
