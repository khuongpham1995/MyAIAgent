/**
 * MyAIAgent | Orchestrator · Application
 *
 * Represents role/content history passed from the application layer to the chat provider adapter.
 */

namespace MyAIAgent.Orchestrator.Application.Chat;

public sealed record ChatMessage(string Role, string Content);
