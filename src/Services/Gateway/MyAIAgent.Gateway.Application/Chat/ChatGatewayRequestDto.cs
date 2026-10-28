/**
 * MyAIAgent | Gateway · Application
 *
 * Defines normal Gateway chat input without allowing the caller to supply an owner identity.
 */

namespace MyAIAgent.Gateway.Application.Chat;

public sealed record ChatGatewayRequestDto(string? SessionId, string UserMessage, string? AgentId);
