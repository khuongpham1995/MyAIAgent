/**
 * MyAIAgent | Orchestrator · Application
 *
 * Defines owner-scoped session lookup, listing, addition, and deletion for conversation handlers.
 */

using MyAIAgent.Orchestrator.Application.Conversations;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Application.Common.Interfaces;

public interface IChatSessionRepository
{
    // Sessions are partitioned by user, so a lookup always names the owner; another user's session is simply not found.
    Task<ChatSession?> FindForUserAsync(string sessionId, string userId, CancellationToken cancellationToken);
    Task AddAsync(ChatSession session, CancellationToken cancellationToken);
    void Delete(ChatSession session);
    Task<IReadOnlyList<ConversationSummary>> ListSummariesByUserAsync(
        string userId, int skip, int take, CancellationToken cancellationToken);
}
