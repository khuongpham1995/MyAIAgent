using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Domain.Interfaces;

public interface IChatSessionRepository
{
    Task<ChatSession?> FindBySessionIdAsync(string sessionId, CancellationToken cancellationToken);
    Task AddAsync(ChatSession session, CancellationToken cancellationToken);
    Task UpdateAsync(ChatSession session, CancellationToken cancellationToken);
}
