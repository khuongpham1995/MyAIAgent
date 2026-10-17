using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Application.Common.Interfaces;

public interface IChatSessionRepository
{
    Task<ChatSession?> FindBySessionIdAsync(string sessionId, CancellationToken cancellationToken);
    Task AddAsync(ChatSession session, CancellationToken cancellationToken);
}
