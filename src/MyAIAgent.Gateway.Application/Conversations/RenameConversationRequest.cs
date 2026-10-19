namespace MyAIAgent.Gateway.Application.Conversations;

public sealed record RenameConversationRequest(string SessionId, string UserId, RenameConversationRequestDto Body);
