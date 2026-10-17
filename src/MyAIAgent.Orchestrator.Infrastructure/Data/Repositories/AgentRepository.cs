using Microsoft.EntityFrameworkCore;
using MyAIAgent.Common.Domain.Common;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Infrastructure.Data.Repositories;

public sealed class AgentRepository(OrchestratorDbContext context) : IAgentRepository, IScoped
{
    public async Task<AgentDefinition?> FindByIdAsync(string id, CancellationToken cancellationToken)
        => await context.AgentDefinitions.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<AgentDefinition?> FindDefaultAsync(CancellationToken cancellationToken)
        => await context.AgentDefinitions.FirstOrDefaultAsync(a => a.IsDefault, cancellationToken);

    public async Task AddAsync(AgentDefinition agent, CancellationToken cancellationToken)
    {
        await context.AgentDefinitions.AddAsync(agent, cancellationToken);
    }

    public void Delete(AgentDefinition agent) => context.AgentDefinitions.Remove(agent);

    public async Task<IReadOnlyList<AgentDefinition>> ListAllAsync(CancellationToken cancellationToken)
        => await context.AgentDefinitions.ToListAsync(cancellationToken);
}
