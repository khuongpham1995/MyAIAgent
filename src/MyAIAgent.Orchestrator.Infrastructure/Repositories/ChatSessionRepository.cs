using Microsoft.EntityFrameworkCore;
using MyAIAgent.Common.Domain.ServiceLifetime;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Application.Interfaces;
using MyAIAgent.Orchestrator.Infrastructure.Data;

namespace MyAIAgent.Orchestrator.Infrastructure.Repositories;

public sealed class ChatSessionRepository(OrchestratorDbContext context)
    : IChatSessionRepository, IScoped
{
    public async Task<ChatSession?> FindBySessionIdAsync(string sessionId, CancellationToken cancellationToken)
        => await context.ChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);

    public async Task AddAsync(ChatSession session, CancellationToken cancellationToken)
    {
        await context.ChatSessions.AddAsync(session, cancellationToken);
    }
}
