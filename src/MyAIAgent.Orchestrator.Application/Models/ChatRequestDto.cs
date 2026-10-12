namespace MyAIAgent.Orchestrator.Application.Models;

public sealed record ChatRequestDto(string? SessionId, string UserMessage, string UserId, string? AgentId = null);
