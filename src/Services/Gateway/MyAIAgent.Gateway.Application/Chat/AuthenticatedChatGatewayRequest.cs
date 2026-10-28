/**
 * MyAIAgent | Gateway · Application
 *
 * Pairs caller-supplied chat input with a server-resolved user identity before mapping to an application request.
 */

namespace MyAIAgent.Gateway.Application.Chat;

public sealed record AuthenticatedChatGatewayRequest(ChatGatewayRequestDto Request, string UserId);
