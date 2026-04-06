namespace MyAIAgent.Orchestrator.Application.Models;

public sealed record ChatRequestDto(string? SessionId, string UserMessage, string? AgentId = null);
