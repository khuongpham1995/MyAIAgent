namespace MyAIAgent.Gateway.Application.Conversations;

public sealed record ConversationSummary(string SessionId, string Title, DateTimeOffset UpdatedAt);
