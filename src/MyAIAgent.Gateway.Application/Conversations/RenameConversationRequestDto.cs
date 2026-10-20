/**
 * MyAIAgent | Gateway · Application
 *
 * Defines the incoming conversation rename body at the owning HTTP boundary.
 */

namespace MyAIAgent.Gateway.Application.Conversations;

public sealed record RenameConversationRequestDto(string Title);
