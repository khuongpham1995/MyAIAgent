/**
 * MyAIAgent | Orchestrator · Application
 *
 * Combines the route session identifier with the trusted caller's owner/title body for application mapping.
 */

namespace MyAIAgent.Orchestrator.Application.Conversations;

public sealed record RenameConversationRequest(string SessionId, RenameConversationRequestDto Body);
