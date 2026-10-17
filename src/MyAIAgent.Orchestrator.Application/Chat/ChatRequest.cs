namespace MyAIAgent.Orchestrator.Application.Chat;

public sealed record ChatRequest(string UserMessage, IReadOnlyList<ChatMessage> History, string? AgentId = null);
