/**
 * MyAIAgent | Orchestrator · Application
 *
 * Queries a paginated list of conversations belonging to the requesting user.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Conversations.Queries.ListConversations;

public sealed class ListConversationsHandler(IChatSessionRepository sessionRepository)
    : IRequestHandler<ListConversationsQuery, ResponseModel<IReadOnlyList<ConversationSummary>>>
{
    public async Task<ResponseModel<IReadOnlyList<ConversationSummary>>> Handle(
        ListConversationsQuery query,
        CancellationToken cancellationToken)
    {
        var conversations = await sessionRepository.ListSummariesByUserAsync(
            query.UserId,
            (query.Page - 1) * query.PageSize,
            query.PageSize,
            cancellationToken);

        return ResponseModel<IReadOnlyList<ConversationSummary>>.Succeed(conversations);
    }
}
