using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Conversations.Commands.DeleteConversation;

public sealed record DeleteConversationCommand(string SessionId, string UserId)
    : IRequest<ResponseModel<bool>>;
