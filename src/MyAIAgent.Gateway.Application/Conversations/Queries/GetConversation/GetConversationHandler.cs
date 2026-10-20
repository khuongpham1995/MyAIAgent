/**
 * MyAIAgent | Gateway · Application
 *
 * Forwards owner-scoped conversation retrieval with trusted owner identity to Orchestrator.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Application.Common.Interfaces;

namespace MyAIAgent.Gateway.Application.Conversations.Queries.GetConversation;

public sealed class GetConversationHandler(IOrchestratorClient orchestratorClient)
    : IRequestHandler<GetConversationQuery, ResponseModel<ConversationDetail>>
{
    public async Task<ResponseModel<ConversationDetail>> Handle(
        GetConversationQuery query,
        CancellationToken cancellationToken) =>
        await orchestratorClient.GetConversationAsync(query.SessionId, query.UserId, cancellationToken);
}
