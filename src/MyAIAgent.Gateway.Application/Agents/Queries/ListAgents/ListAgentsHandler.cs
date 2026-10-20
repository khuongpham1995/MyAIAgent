/**
 * MyAIAgent | Gateway · Application
 *
 * Forwards agent listing to Orchestrator through the Gateway client port.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Application.Common.Interfaces;

namespace MyAIAgent.Gateway.Application.Agents.Queries.ListAgents;

public sealed class ListAgentsHandler(IOrchestratorClient orchestratorClient)
    : IRequestHandler<ListAgentsQuery, ResponseModel<IReadOnlyList<AgentSummary>>>
{
    public async Task<ResponseModel<IReadOnlyList<AgentSummary>>> Handle(
        ListAgentsQuery query,
        CancellationToken cancellationToken) =>
        await orchestratorClient.ListAgentsAsync(cancellationToken);
}
