/**
 * MyAIAgent | Orchestrator · Application
 *
 * Defines Orchestrator chat HTTP input, including the owner identity supplied by its trusted caller.
 */

namespace MyAIAgent.Orchestrator.Application.Chat;

public sealed record ChatRequestDto(string? SessionId, string UserMessage, string UserId, string? AgentId = null);
