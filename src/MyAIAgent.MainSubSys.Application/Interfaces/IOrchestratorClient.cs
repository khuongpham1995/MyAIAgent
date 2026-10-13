using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Interfaces;

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
}
