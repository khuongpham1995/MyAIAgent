using MyAIAgent.Common.Domain.ServiceLifetime;
using MyAIAgent.Orchestrator.Application.Interfaces;

namespace MyAIAgent.Orchestrator.Infrastructure.Data;

public sealed class OrchestratorUnitOfWork(OrchestratorDbContext context) : IUnitOfWork, IScoped
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
