namespace MyAIAgent.Gateway.Application.Chat;

public sealed record AuthenticatedChatGatewayRequest(ChatGatewayRequestDto Request, string UserId);
