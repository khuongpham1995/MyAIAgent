namespace MyAIAgent.MainSubSys.Application.Chat;

public sealed record AuthenticatedChatGatewayRequest(ChatGatewayRequestDto Request, string UserId);
