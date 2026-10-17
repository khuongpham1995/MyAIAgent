namespace MyAIAgent.MainSubSys.Application.Models;

public sealed record AuthenticatedChatGatewayRequest(ChatGatewayRequestDto Request, string UserId);
