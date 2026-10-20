/**
 * MyAIAgent | Orchestrator · Application
 *
 * Defines application access to agent definitions independently of the Cosmos persistence adapter.
 */

using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Application.Common.Interfaces;

public interface IAgentRepository
{
    Task<AgentDefinition?> FindByIdAsync(string id, CancellationToken cancellationToken);
    Task<AgentDefinition?> FindDefaultAsync(CancellationToken cancellationToken);
    Task AddAsync(AgentDefinition agent, CancellationToken cancellationToken);
    void Delete(AgentDefinition agent);
    Task<IReadOnlyList<AgentDefinition>> ListAllAsync(CancellationToken cancellationToken);
}
