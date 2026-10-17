namespace MyAIAgent.MainSubSys.Application.Chat;

public sealed record ChatGatewayRequestDto(string? SessionId, string UserMessage, string? AgentId);
