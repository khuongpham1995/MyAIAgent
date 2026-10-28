/**
 * MyAIAgent | Gateway · Application
 *
 * Combines visitor input with server-selected identity and agent settings for public chat dispatch.
 */

namespace MyAIAgent.Gateway.Application.Chat;

// AgentId comes from server configuration so a public caller can never pick the agent.
public sealed record PublicChatGatewayRequest(PublicChatRequestDto Request, string UserId, string? AgentId);
