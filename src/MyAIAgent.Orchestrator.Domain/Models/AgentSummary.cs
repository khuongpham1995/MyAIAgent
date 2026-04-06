namespace MyAIAgent.Orchestrator.Domain.Models;

public sealed record AgentSummary(
    string Id,
    string Name,
    string Instructions,
    string McpServerEndpoint,
    bool IsDefault,
    IReadOnlyList<McpToolInfo> EnabledTools);
