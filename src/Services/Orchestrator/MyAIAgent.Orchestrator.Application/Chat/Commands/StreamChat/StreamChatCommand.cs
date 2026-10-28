/**
 * MyAIAgent | Orchestrator · Application
 *
 * Carries a persisted streaming chat turn input through the Orchestrator MediatR application boundary.
 */

using MediatR;

namespace MyAIAgent.Orchestrator.Application.Chat.Commands.StreamChat;

public sealed record StreamChatCommand(string? SessionId, string UserMessage, string UserId, string? AgentId = null)
    : IStreamRequest<ChatStreamEvent>;
