using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Application.Mappers;
using MyAIAgent.Orchestrator.Application.Interfaces;
using MyAIAgent.Orchestrator.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Handlers.Agents;

public sealed class ListAgentsHandler(IAgentRepository agentRepository)
    : IRequestHandler<ListAgentsQuery, ResponseModel<IReadOnlyList<AgentSummary>>>
{
    public async Task<ResponseModel<IReadOnlyList<AgentSummary>>> Handle(
        ListAgentsQuery query,
        CancellationToken cancellationToken)
    {
        var agents = await agentRepository.ListAllAsync(cancellationToken);
        IReadOnlyList<AgentSummary> summaries = agents.Select(a => a.ToSummary()).ToList();
        return ResponseModel<IReadOnlyList<AgentSummary>>.Succeed(summaries);
    }
}
