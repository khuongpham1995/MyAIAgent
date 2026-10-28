/**
 * MyAIAgent | Orchestrator · Application
 *
 * Carries a persisted chat turn input through the Orchestrator MediatR application boundary.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.Orchestrator.Application.Chat.Commands.Chat;

public sealed record ChatCommand(string? SessionId, string UserMessage, string UserId, string? AgentId = null)
    : IRequest<ResponseModel<ChatResponse>>;
