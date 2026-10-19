namespace MyAIAgent.Gateway.Application.Chat;

public sealed record ChatGatewayRequestDto(string? SessionId, string UserMessage, string? AgentId);
