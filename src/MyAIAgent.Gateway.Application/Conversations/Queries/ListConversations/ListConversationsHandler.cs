/**
 * MyAIAgent | Gateway · Application
 *
 * Forwards paginated conversation listing with trusted owner identity to Orchestrator.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Application.Common.Interfaces;

namespace MyAIAgent.Gateway.Application.Conversations.Queries.ListConversations;

public sealed class ListConversationsHandler(IOrchestratorClient orchestratorClient)
    : IRequestHandler<ListConversationsQuery, ResponseModel<IReadOnlyList<ConversationSummary>>>
{
    public async Task<ResponseModel<IReadOnlyList<ConversationSummary>>> Handle(
        ListConversationsQuery query,
        CancellationToken cancellationToken) =>
        await orchestratorClient.ListConversationsAsync(query.UserId, query.Page, query.PageSize, cancellationToken);
}
