/**
 * MyAIAgent | Gateway · Application
 *
 * Represents a stored message as role, content, and timestamp for conversation responses.
 */

namespace MyAIAgent.Gateway.Application.Conversations;

public sealed record ConversationMessage(string Role, string Content, DateTimeOffset CreatedAt);
