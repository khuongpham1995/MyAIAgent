using MyAIAgent.Common.Domain.Abstractions;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Infrastructure.Data.Repositories;

public sealed class OrchestratorUnitOfWork(OrchestratorDbContext context) : IUnitOfWork, IScoped
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
