namespace MyAIAgent.Gateway.Application.Conversations;

public sealed record ConversationDetail(
    string SessionId,
    string Title,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ConversationMessage> Messages);
