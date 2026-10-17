using MediatR;
using MapsterMapper;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Application.Interfaces;
using MyAIAgent.Orchestrator.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Agents.Queries.ListAgents;

public sealed class ListAgentsHandler(IAgentRepository agentRepository, IMapper mapper)
    : IRequestHandler<ListAgentsQuery, ResponseModel<IReadOnlyList<AgentSummary>>>
{
    public async Task<ResponseModel<IReadOnlyList<AgentSummary>>> Handle(
        ListAgentsQuery query,
        CancellationToken cancellationToken)
    {
        var agents = await agentRepository.ListAllAsync(cancellationToken);
        IReadOnlyList<AgentSummary> summaries = agents.Select(mapper.Map<AgentSummary>).ToList();
        return ResponseModel<IReadOnlyList<AgentSummary>>.Succeed(summaries);
    }
}
