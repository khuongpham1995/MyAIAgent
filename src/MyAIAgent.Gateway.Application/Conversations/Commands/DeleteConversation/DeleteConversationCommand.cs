/**
 * MyAIAgent | Gateway · Application
 *
 * Carries owner-scoped conversation deletion input through the Gateway MediatR application boundary.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Gateway.Application.Conversations.Commands.DeleteConversation;

public sealed record DeleteConversationCommand(string SessionId, string UserId)
    : IRequest<ResponseModel<bool>>;
