namespace MyAIAgent.Orchestrator.Application.Conversations;

public sealed record ConversationDetail(
    string SessionId,
    string Title,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ConversationMessage> Messages);
