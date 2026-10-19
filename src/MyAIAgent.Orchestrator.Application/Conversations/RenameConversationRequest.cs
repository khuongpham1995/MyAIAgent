namespace MyAIAgent.Orchestrator.Application.Conversations;

public sealed record RenameConversationRequest(string SessionId, RenameConversationRequestDto Body);
