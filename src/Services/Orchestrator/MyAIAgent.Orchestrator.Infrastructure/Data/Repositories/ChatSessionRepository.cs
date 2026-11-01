/**
 * MyAIAgent | Orchestrator · Infrastructure
 *
 * Implements owner-scoped Cosmos conversation queries and tracks session changes for the unit of work.
 */

using Microsoft.EntityFrameworkCore;
using MyAIAgent.Common.Domain.Abstractions;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;
using MyAIAgent.Orchestrator.Application.Conversations;

namespace MyAIAgent.Orchestrator.Infrastructure.Data.Repositories;

public sealed class ChatSessionRepository(OrchestratorDbContext context)
    : IChatSessionRepository, IScoped
{
    public async Task<ChatSession?> FindForUserAsync(string sessionId, string userId, CancellationToken cancellationToken)
        => await context.ChatSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SessionId == sessionId, cancellationToken);

    public async Task AddAsync(ChatSession session, CancellationToken cancellationToken)
    {
        await context.ChatSessions.AddAsync(session, cancellationToken);
    }

    public void Delete(ChatSession session) => context.ChatSessions.Remove(session);

    public async Task<IReadOnlyList<ConversationSummary>> ListSummariesByUserAsync(
        string userId, int skip, int take, CancellationToken cancellationToken)
        => await context.ChatSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .Select(s => new ConversationSummary(s.SessionId, s.Title, s.UpdatedAt))
            .ToListAsync(cancellationToken);
}
