/**
 * MyAIAgent | Orchestrator · Application
 *
 * Lets agent validation apply configured MCP destination permissions without depending on HTTP infrastructure.
 */

namespace MyAIAgent.Orchestrator.Application.Common.Interfaces;

public interface IMcpEndpointPolicy
{
    bool IsAllowed(string? endpoint);
}
