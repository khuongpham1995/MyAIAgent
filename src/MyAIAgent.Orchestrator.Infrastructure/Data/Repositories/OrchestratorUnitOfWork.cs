using Microsoft.EntityFrameworkCore;
using MyAIAgent.Common.Domain.Abstractions;
using MyAIAgent.Orchestrator.Application.Common.Exceptions;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Infrastructure.Data.Repositories;

public sealed class OrchestratorUnitOfWork(OrchestratorDbContext context) : IUnitOfWork, IScoped
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // EF wraps Cosmos failures (throttling, conflicts, transient errors) in DbUpdateException.
            throw new PersistenceException("Saving changes failed.", ex);
        }
    }
}
