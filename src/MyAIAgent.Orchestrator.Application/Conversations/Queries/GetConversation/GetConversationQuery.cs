using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Conversations.Queries.GetConversation;

public sealed record GetConversationQuery(string SessionId, string UserId)
    : IRequest<ResponseModel<ConversationDetail>>;
