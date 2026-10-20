/**
 * MyAIAgent | Gateway · Application
 *
 * Provides the compact conversation metadata used in paginated lists.
 */

namespace MyAIAgent.Gateway.Application.Conversations;

public sealed record ConversationSummary(string SessionId, string Title, DateTimeOffset UpdatedAt);
