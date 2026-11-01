/**
 * MyAIAgent | Orchestrator · Infrastructure
 *
 * Applies the startup MCP origin configuration to agent creation without making network requests.
 */

using MyAIAgent.Common.Domain.Abstractions;
using MyAIAgent.Common.Infrastructure.Http;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Infrastructure.Services;

public sealed class McpEndpointPolicy(McpServiceClientSettings settings) : IMcpEndpointPolicy, ISingleton
{
    public bool IsAllowed(string? endpoint) => settings.IsAllowedEndpoint(endpoint);
}
