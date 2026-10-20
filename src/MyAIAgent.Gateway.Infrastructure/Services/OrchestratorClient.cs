/**
 * MyAIAgent | Gateway · Infrastructure
 *
 * Implements Gateway JSON calls to Orchestrator for chat, agents, and conversations with cancellation propagation.
 */

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Domain.Constants;
using MyAIAgent.Gateway.Application.Common.Interfaces;
using MyAIAgent.Gateway.Application.Agents;
using MyAIAgent.Gateway.Application.Chat;
using MyAIAgent.Gateway.Application.Conversations;

namespace MyAIAgent.Gateway.Infrastructure.Services;

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
                return ResponseModel<ChatGatewayResponse>.Fail(message: GatewayConstant.Errors.ChatFailed);

            return ResponseModel<ChatGatewayResponse>.Succeed(
                new ChatGatewayResponse(result.Data.Reply, result.Data.SessionId));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Orchestrator chat request failed");
            return ResponseModel<ChatGatewayResponse>.Fail(message: GatewayConstant.Errors.ChatFailed);
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
            return ResponseModel<IReadOnlyList<AgentSummary>>.Fail(message: GatewayConstant.Errors.AgentListFailed);
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
            return ResponseModel<string>.Fail(message: GatewayConstant.Errors.AgentCreationFailed);
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
            return ResponseModel<bool>.Fail(message: GatewayConstant.Errors.AgentDeletionFailed);
        }
    }

    public async Task<ResponseModel<IReadOnlyList<ConversationSummary>>> ListConversationsAsync(
        string userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"/conversations?userId={Uri.EscapeDataString(userId)}&page={page}&pageSize={pageSize}";
            var response = await httpClient.GetFromJsonAsync<OrchestratorConversationListResult>(url, cancellationToken);

            IReadOnlyList<ConversationSummary> conversations = response?.Data ?? [];
            return ResponseModel<IReadOnlyList<ConversationSummary>>.Succeed(conversations);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Orchestrator list conversations request failed");
            return ResponseModel<IReadOnlyList<ConversationSummary>>.Fail(
                message: GatewayConstant.Errors.ConversationListFailed);
        }
    }

    public async Task<ResponseModel<ConversationDetail>> GetConversationAsync(
        string sessionId, string userId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync(
                $"/conversations/{Uri.EscapeDataString(sessionId)}?userId={Uri.EscapeDataString(userId)}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return ResponseModel<ConversationDetail>.Fail(message: GatewayConstant.Errors.ConversationNotFound);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OrchestratorConversationDetailResult>(
                cancellationToken: cancellationToken);

            return result?.Data is null
                ? ResponseModel<ConversationDetail>.Fail(message: GatewayConstant.Errors.ConversationLoadFailed)
                : ResponseModel<ConversationDetail>.Succeed(result.Data);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Orchestrator get conversation request failed for session {SessionId}", sessionId);
            return ResponseModel<ConversationDetail>.Fail(message: GatewayConstant.Errors.ConversationLoadFailed);
        }
    }

    public async Task<ResponseModel<bool>> RenameConversationAsync(
        string sessionId, string userId, string title, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync(
                $"/conversations/{Uri.EscapeDataString(sessionId)}/title",
                new { userId, title },
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return ResponseModel<bool>.Fail(message: GatewayConstant.Errors.ConversationNotFound);

            response.EnsureSuccessStatusCode();
            return ResponseModel<bool>.Succeed(true);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Orchestrator rename conversation request failed for session {SessionId}", sessionId);
            return ResponseModel<bool>.Fail(message: GatewayConstant.Errors.ConversationRenameFailed);
        }
    }

    public async Task<ResponseModel<bool>> DeleteConversationAsync(
        string sessionId, string userId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.DeleteAsync(
                $"/conversations/{Uri.EscapeDataString(sessionId)}?userId={Uri.EscapeDataString(userId)}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return ResponseModel<bool>.Fail(message: GatewayConstant.Errors.ConversationNotFound);

            response.EnsureSuccessStatusCode();
            return ResponseModel<bool>.Succeed(true);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Orchestrator delete conversation request failed for session {SessionId}", sessionId);
            return ResponseModel<bool>.Fail(message: GatewayConstant.Errors.ConversationDeletionFailed);
        }
    }

    private sealed record OrchestratorConversationListResult(
        [property: JsonPropertyName("data")] List<ConversationSummary>? Data);

    private sealed record OrchestratorConversationDetailResult(
        [property: JsonPropertyName("data")] ConversationDetail? Data);

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
