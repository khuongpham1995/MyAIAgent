/**
 * MyAIAgent | Gateway · Application
 *
 * Limits public visitor chat input to a message and optional session identifier.
 */

namespace MyAIAgent.Gateway.Application.Chat;

public sealed record PublicChatRequestDto(string? SessionId, string UserMessage);
