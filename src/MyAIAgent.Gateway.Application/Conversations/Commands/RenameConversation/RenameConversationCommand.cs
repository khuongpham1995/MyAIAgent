using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Gateway.Application.Conversations.Commands.RenameConversation;

public sealed record RenameConversationCommand(string SessionId, string UserId, string Title)
    : IRequest<ResponseModel<bool>>;
