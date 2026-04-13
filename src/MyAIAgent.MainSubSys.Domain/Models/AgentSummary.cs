namespace MyAIAgent.MainSubSys.Domain.Models;

public sealed record AgentSummary(
    string Id,
    string Name,
    string Instructions,
    string McpServerEndpoint,
    bool IsDefault,
    IReadOnlyList<string> EnabledTools);
