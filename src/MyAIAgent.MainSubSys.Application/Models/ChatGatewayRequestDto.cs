namespace MyAIAgent.MainSubSys.Application.Models;

public sealed record ChatGatewayRequestDto(string? SessionId, string UserMessage, string? AgentId);
