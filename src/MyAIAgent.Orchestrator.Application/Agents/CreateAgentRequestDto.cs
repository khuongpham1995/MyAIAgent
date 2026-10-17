namespace MyAIAgent.Orchestrator.Application.Agents;

public sealed record CreateAgentRequestDto(
    string Name,
    string Instructions,
    string McpServerEndpoint,
    bool IsDefault,
    IReadOnlyList<string>? EnabledTools = null);
