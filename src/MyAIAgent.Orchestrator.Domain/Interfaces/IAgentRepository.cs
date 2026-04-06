using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Domain.Interfaces;

public interface IAgentRepository
{
    Task<AgentDefinition?> FindByIdAsync(string id, CancellationToken cancellationToken);
    Task<AgentDefinition?> FindDefaultAsync(CancellationToken cancellationToken);
    Task AddAsync(AgentDefinition agent, CancellationToken cancellationToken);
    Task DeleteAsync(AgentDefinition agent, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentDefinition>> ListAllAsync(CancellationToken cancellationToken);
}
