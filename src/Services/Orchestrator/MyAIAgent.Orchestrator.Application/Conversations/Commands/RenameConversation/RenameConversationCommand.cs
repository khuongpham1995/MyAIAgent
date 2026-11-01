/**
 * MyAIAgent | Orchestrator · Application
 *
 * Carries owner-scoped conversation renaming input through the Orchestrator MediatR application boundary.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.Orchestrator.Application.Conversations.Commands.RenameConversation;

public sealed record RenameConversationCommand(string SessionId, string UserId, string Title)
    : IRequest<ResponseModel<bool>>;
