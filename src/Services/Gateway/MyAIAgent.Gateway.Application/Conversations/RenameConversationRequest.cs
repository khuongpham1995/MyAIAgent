/**
 * MyAIAgent | Gateway · Application
 *
 * Combines the route session identifier, trusted owner identity, and rename body for application mapping.
 */

namespace MyAIAgent.Gateway.Application.Conversations;

public sealed record RenameConversationRequest(string SessionId, string UserId, RenameConversationRequestDto Body);
