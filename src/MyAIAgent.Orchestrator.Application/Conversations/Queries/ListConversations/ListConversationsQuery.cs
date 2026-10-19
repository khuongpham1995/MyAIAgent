using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Conversations.Queries.ListConversations;

public sealed record ListConversationsQuery(string UserId, int Page = 1, int PageSize = 20)
    : IRequest<ResponseModel<IReadOnlyList<ConversationSummary>>>;
