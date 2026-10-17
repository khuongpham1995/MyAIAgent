using Microsoft.EntityFrameworkCore;
using MyAIAgent.Common.Domain.Abstractions;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Infrastructure.Data.Repositories;

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
