/**
 * MyAIAgent | Gateway · Application
 *
 * Defines Gateway operations for downstream chat, agent administration, and owner-scoped conversations.
 */

using MyAIAgent.Common.Application.Models;
using MyAIAgent.Gateway.Application.Agents;
using MyAIAgent.Gateway.Application.Chat;
using MyAIAgent.Gateway.Application.Conversations;

namespace MyAIAgent.Gateway.Application.Common.Interfaces;

public interface IOrchestratorClient
{
    Task<ResponseModel<ChatGatewayResponse>> ChatAsync(
        string? sessionId,
        string userMessage,
        string userId,
        string? agentId,
        CancellationToken cancellationToken);

    Task<ResponseModel<IReadOnlyList<AgentSummary>>> ListAgentsAsync(CancellationToken cancellationToken);

    Task<ResponseModel<string>> CreateAgentAsync(
        string name,
        string instructions,
        string? mcpServerEndpoint,
        bool isDefault,
        IReadOnlyList<string>? enabledTools,
        CancellationToken cancellationToken);

    Task<ResponseModel<bool>> DeleteAgentAsync(string agentId, CancellationToken cancellationToken);

    Task<ResponseModel<IReadOnlyList<ConversationSummary>>> ListConversationsAsync(
        string userId, int page, int pageSize, CancellationToken cancellationToken);

    Task<ResponseModel<ConversationDetail>> GetConversationAsync(
        string sessionId, string userId, CancellationToken cancellationToken);

    Task<ResponseModel<bool>> RenameConversationAsync(
        string sessionId, string userId, string title, CancellationToken cancellationToken);

    Task<ResponseModel<bool>> DeleteConversationAsync(
        string sessionId, string userId, CancellationToken cancellationToken);
}
