using Microsoft.EntityFrameworkCore;
using MyAIAgent.Common.Domain.ServiceLifetime;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Domain.Interfaces;
using MyAIAgent.Orchestrator.Infrastructure.Data;

namespace MyAIAgent.Orchestrator.Infrastructure.Repositories;

public sealed class AgentRepository(OrchestratorDbContext context) : IAgentRepository, IScoped
{
    public async Task<AgentDefinition?> FindByIdAsync(string id, CancellationToken cancellationToken)
        => await context.AgentDefinitions.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<AgentDefinition?> FindDefaultAsync(CancellationToken cancellationToken)
        => await context.AgentDefinitions.FirstOrDefaultAsync(a => a.IsDefault, cancellationToken);

    public async Task AddAsync(AgentDefinition agent, CancellationToken cancellationToken)
    {
        context.AgentDefinitions.Add(agent);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(AgentDefinition agent, CancellationToken cancellationToken)
    {
        context.AgentDefinitions.Remove(agent);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentDefinition>> ListAllAsync(CancellationToken cancellationToken)
        => await context.AgentDefinitions.ToListAsync(cancellationToken);
}
