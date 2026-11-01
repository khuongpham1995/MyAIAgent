/**
 * MyAIAgent | Orchestrator · Application
 *
 * Exposes the commit boundary for changes made by Orchestrator repositories.
 */

namespace MyAIAgent.Orchestrator.Application.Common.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
