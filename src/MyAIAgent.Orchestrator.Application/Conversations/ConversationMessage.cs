namespace MyAIAgent.Orchestrator.Application.Conversations;

public sealed record ConversationMessage(string Role, string Content, DateTimeOffset CreatedAt);
