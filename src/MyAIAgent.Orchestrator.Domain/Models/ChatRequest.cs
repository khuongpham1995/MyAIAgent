namespace MyAIAgent.Orchestrator.Domain.Models;

public sealed record ChatRequest(string UserMessage, IReadOnlyList<ChatMessage> History, string? AgentId = null);
