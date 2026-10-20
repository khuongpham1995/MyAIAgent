/**
 * MyAIAgent | Orchestrator · Application
 *
 * Bundles the current message, prepared history, and optional agent selection for chat orchestration.
 */

namespace MyAIAgent.Orchestrator.Application.Chat;

public sealed record ChatRequest(string UserMessage, IReadOnlyList<ChatMessage> History, string? AgentId = null);
