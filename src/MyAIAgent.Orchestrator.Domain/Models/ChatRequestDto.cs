namespace MyAIAgent.Orchestrator.Domain.Models;

public sealed record ChatRequestDto(string? SessionId, string UserMessage);
