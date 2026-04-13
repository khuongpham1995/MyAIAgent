namespace MyAIAgent.MainSubSys.Application.Models;

public sealed record CreateAgentRequestDto(
    string Name,
    string Instructions,
    string? McpServerEndpoint,
    bool IsDefault,
    IReadOnlyList<string>? EnabledTools = null);
