using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Common.Interfaces;

namespace MyAIAgent.MainSubSys.Application.Agents.Queries.ListAgents;

public sealed class ListAgentsHandler(IOrchestratorClient orchestratorClient)
    : IRequestHandler<ListAgentsQuery, ResponseModel<IReadOnlyList<AgentSummary>>>
{
    public async Task<ResponseModel<IReadOnlyList<AgentSummary>>> Handle(
        ListAgentsQuery query,
        CancellationToken cancellationToken) =>
        await orchestratorClient.ListAgentsAsync(cancellationToken);
}
