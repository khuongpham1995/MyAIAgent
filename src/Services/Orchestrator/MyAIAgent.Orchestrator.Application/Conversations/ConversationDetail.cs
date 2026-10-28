/**
 * MyAIAgent | Orchestrator · Application
 *
 * Represents a conversation with its metadata and messages for application responses.
 */

namespace MyAIAgent.Orchestrator.Application.Conversations;

public sealed record ConversationDetail(
    string SessionId,
    string Title,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ConversationMessage> Messages);
