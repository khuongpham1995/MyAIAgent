namespace MyAIAgent.Orchestrator.Application.Chat;

public sealed record ChatRequestDto(string? SessionId, string UserMessage, string UserId, string? AgentId = null);
