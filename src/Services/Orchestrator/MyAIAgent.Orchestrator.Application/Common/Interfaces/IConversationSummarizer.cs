/**
 * MyAIAgent | Orchestrator · Application
 *
 * Defines best-effort condensation of older messages into rolling conversation context.
 */

using MyAIAgent.Orchestrator.Application.Chat;

namespace MyAIAgent.Orchestrator.Application.Common.Interfaces;

public interface IConversationSummarizer
{
    /// <summary>Returns the updated summary, or null when summarization is unavailable.</summary>
    Task<string?> SummarizeAsync(
        string? previousSummary,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken);
}
