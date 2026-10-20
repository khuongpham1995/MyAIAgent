/**
 * MyAIAgent | Orchestrator · Application
 *
 * Returns the generated reply together with the persisted conversation identifier.
 */

namespace MyAIAgent.Orchestrator.Application.Chat;

public sealed record ChatResponse(string Reply, string SessionId);
