namespace MyAIAgent.MainSubSys.Application.Agents;

public sealed record AgentSummary(
    string Id,
    string Name,
    string Instructions,
    string McpServerEndpoint,
    bool IsDefault,
    IReadOnlyList<string> EnabledTools);
