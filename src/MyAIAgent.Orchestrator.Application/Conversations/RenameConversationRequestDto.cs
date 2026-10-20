/**
 * MyAIAgent | Orchestrator · Application
 *
 * Defines the incoming conversation rename body at the owning HTTP boundary.
 */

namespace MyAIAgent.Orchestrator.Application.Conversations;

public sealed record RenameConversationRequestDto(string UserId, string Title);
