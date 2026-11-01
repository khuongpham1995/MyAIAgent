/**
 * MyAIAgent | Orchestrator · Application
 *
 * Lists persisted agent definitions and maps their enabled tools to response summaries.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;
using MapsterMapper;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Agents.Queries.ListAgents;

public sealed class ListAgentsHandler(IAgentRepository agentRepository, IMapper mapper)
    : IRequestHandler<ListAgentsQuery, ResponseModel<IReadOnlyList<AgentSummary>>>
{
    public async Task<ResponseModel<IReadOnlyList<AgentSummary>>> Handle(
        ListAgentsQuery query,
        CancellationToken cancellationToken)
    {
        var agents = await agentRepository.ListAllAsync(cancellationToken);
        IReadOnlyList<AgentSummary> summaries = [.. agents.Select(mapper.Map<AgentSummary>)];
        return ResponseModel<IReadOnlyList<AgentSummary>>.Succeed(summaries);
    }
}
