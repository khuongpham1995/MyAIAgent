using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Domain.Constants;
using MyAIAgent.MainSubSys.Application.Common.Interfaces;
using MyAIAgent.MainSubSys.Application.Agents;
using MyAIAgent.MainSubSys.Application.Chat;

namespace MyAIAgent.MainSubSys.Infrastructure.Services;

public sealed class OrchestratorClient(
    HttpClient httpClient,
    ILogger<OrchestratorClient> logger) : IOrchestratorClient
{
    public async Task<ResponseModel<ChatGatewayResponse>> ChatAsync(
        string? sessionId,
        string userMessage,
        string userId,
        string? agentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "/chat",
                new { sessionId, userMessage, userId, agentId },
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OrchestratorChatResult>(
                cancellationToken: cancellationToken);

            if (result?.Data is null)
                return ResponseModel<ChatGatewayResponse>.Fail(message: MainSubSysConstant.Errors.ChatFailed);

            return ResponseModel<ChatGatewayResponse>.Succeed(
                new ChatGatewayResponse(result.Data.Reply, result.Data.SessionId));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Orchestrator chat request failed");
            return ResponseModel<ChatGatewayResponse>.Fail(message: MainSubSysConstant.Errors.ChatFailed);
        }
    }

    public async Task<ResponseModel<IReadOnlyList<AgentSummary>>> ListAgentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<OrchestratorAgentListResult>(
                "/agents", cancellationToken);

            IReadOnlyList<AgentSummary> agents = response?.Data
                ?.Select(a => new AgentSummary(
                    a.Id ?? string.Empty,
                    a.Name ?? string.Empty,
                    a.Instructions ?? string.Empty,
                    a.McpServerEndpoint ?? string.Empty,
                    a.IsDefault,
                    a.EnabledTools?.Select(t => t.Name ?? string.Empty).ToList() ?? []))
                .ToList() ?? [];

            return ResponseModel<IReadOnlyList<AgentSummary>>.Succeed(agents);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Orchestrator list agents request failed");
            return ResponseModel<IReadOnlyList<AgentSummary>>.Fail(message: MainSubSysConstant.Errors.AgentListFailed);
        }
    }

    public async Task<ResponseModel<string>> CreateAgentAsync(
        string name,
        string instructions,
        string? mcpServerEndpoint,
        bool isDefault,
        IReadOnlyList<string>? enabledTools,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "/agents",
                new { name, instructions, mcpServerEndpoint, isDefault, enabledTools },
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OrchestratorAgentCreateResult>(
                cancellationToken: cancellationToken);

            return ResponseModel<string>.Succeed(result?.Data);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Orchestrator create agent request failed for agent {Name}", name);
            return ResponseModel<string>.Fail(message: MainSubSysConstant.Errors.AgentCreationFailed);
        }
    }

    public async Task<ResponseModel<bool>> DeleteAgentAsync(string agentId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"/agents/{agentId}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return ResponseModel<bool>.Fail(message: "Agent not found.");

            response.EnsureSuccessStatusCode();
            return ResponseModel<bool>.Succeed(true);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Orchestrator delete agent request failed for agent {AgentId}", agentId);
            return ResponseModel<bool>.Fail(message: MainSubSysConstant.Errors.AgentDeletionFailed);
        }
    }

    private sealed record OrchestratorChatResult(
        [property: JsonPropertyName("data")] OrchestratorChatData? Data);

    private sealed record OrchestratorChatData(
        [property: JsonPropertyName("reply")] string Reply,
        [property: JsonPropertyName("sessionId")] string SessionId);

    private sealed record OrchestratorAgentListResult(
        [property: JsonPropertyName("data")] List<OrchestratorAgentData>? Data);

    private sealed record OrchestratorAgentData(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("instructions")] string? Instructions,
        [property: JsonPropertyName("mcpServerEndpoint")] string? McpServerEndpoint,
        [property: JsonPropertyName("isDefault")] bool IsDefault,
        [property: JsonPropertyName("enabledTools")] List<OrchestratorToolData>? EnabledTools);

    private sealed record OrchestratorToolData(
        [property: JsonPropertyName("name")] string? Name);

    private sealed record OrchestratorAgentCreateResult(
        [property: JsonPropertyName("data")] string? Data);
}
