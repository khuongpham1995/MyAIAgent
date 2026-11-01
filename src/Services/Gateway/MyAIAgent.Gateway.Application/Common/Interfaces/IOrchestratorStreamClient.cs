/**
 * MyAIAgent | Gateway · Application
 *
 * Defines the Gateway application port for consuming downstream chat events asynchronously.
 */

using MyAIAgent.Gateway.Application.Chat;

namespace MyAIAgent.Gateway.Application.Common.Interfaces;

// Kept apart from IOrchestratorClient because streaming must not be retried once bytes have been sent.
public interface IOrchestratorStreamClient
{
    IAsyncEnumerable<ChatStreamEvent> StreamChatAsync(
        string? sessionId,
        string userMessage,
        string userId,
        string? agentId,
        CancellationToken cancellationToken);
}
