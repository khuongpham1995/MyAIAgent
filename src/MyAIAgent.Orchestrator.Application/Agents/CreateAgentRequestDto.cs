/**
 * MyAIAgent | Orchestrator · Application
 *
 * Defines the HTTP input for agent instructions, MCP endpoint, default selection, and enabled tool names.
 */

namespace MyAIAgent.Orchestrator.Application.Agents;

public sealed record CreateAgentRequestDto(
    string Name,
    string Instructions,
    string McpServerEndpoint,
    bool IsDefault,
    IReadOnlyList<string>? EnabledTools = null);
