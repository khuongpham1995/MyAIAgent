/**
 * MyAIAgent | Orchestrator · Application
 *
 * Carries owner-scoped conversation deletion input through the Orchestrator MediatR application boundary.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.Orchestrator.Application.Conversations.Commands.DeleteConversation;

public sealed record DeleteConversationCommand(string SessionId, string UserId)
    : IRequest<ResponseModel<bool>>;
