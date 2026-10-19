namespace MyAIAgent.Gateway.Application.Conversations;

public sealed record ConversationMessage(string Role, string Content, DateTimeOffset CreatedAt);
