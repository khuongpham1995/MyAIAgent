/**
 * MyAIAgent | Orchestrator · Infrastructure
 *
 * Prepares agent instructions, history, and MCP tools for ordinary or streaming IChatClient replies.
 */

using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Common.Domain.Abstractions;
using MyAIAgent.Orchestrator.Application.Common.Exceptions;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;
using MyAIAgent.Orchestrator.Application.Chat;
using MyAIAgent.Orchestrator.Infrastructure.Helpers;

namespace MyAIAgent.Orchestrator.Infrastructure.Services;

public sealed class ChatOrchestratorService(
    IChatClient chatClient,
    IAgentRepository agentRepository,
    IAgentHelper agentHelper,
    ILogger<ChatOrchestratorService> logger)
    : IChatOrchestratorService, IScoped
{
    public async Task<ResponseModel<string>> ChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var turn = await PrepareTurnAsync(request, cancellationToken);
            var completion = await chatClient.GetResponseAsync(turn.Messages, turn.Options, cancellationToken);
            return ResponseModel<string>.Succeed(completion.Text);
        }
        catch (Exception ex) when (ex is HttpRequestException or ClientResultException)
        {
            logger.LogError(ex, "AI provider request failed");
            return ResponseModel<string>.Fail(message: "AI service temporarily unavailable.");
        }
        catch (AgentNotFoundException ex)
        {
            logger.LogError(ex, "Requested agent was not found");
            return ResponseModel<string>.Fail(message: "Agent not found.");
        }
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var turn = await PrepareTurnAsync(request, cancellationToken);

        await using var updates = chatClient
            .GetStreamingResponseAsync(turn.Messages, turn.Options, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await updates.MoveNextAsync();
            }
            catch (ClientResultException ex)
            {
                // The OpenAI SDK reports throttling and content-filter errors this way, not as HttpRequestException.
                throw new AiProviderException("AI provider request failed.", ex);
            }

            if (!hasNext)
                yield break;

            if (!string.IsNullOrEmpty(updates.Current.Text))
                yield return updates.Current.Text;
        }
    }

    private async Task<ChatTurn> PrepareTurnAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        var messages = request.History
            .Select(m => new Microsoft.Extensions.AI.ChatMessage(
                m.Role switch
                {
                    "assistant" => ChatRole.Assistant,
                    "system" => ChatRole.System,
                    _ => ChatRole.User,
                },
                m.Content))
            .ToList();
        messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, request.UserMessage));

        ChatOptions? chatOptions = null;
        var tools = AgentToolSet.Empty;

        var agent = request.AgentId is not null
            ? await agentRepository.FindByIdAsync(request.AgentId, cancellationToken)
            : await agentRepository.FindDefaultAsync(cancellationToken);

        // An explicitly requested agent that is gone must not silently fall back to an unconstrained model.
        if (agent is null && request.AgentId is not null)
            throw new AgentNotFoundException(request.AgentId);

        if (agent is not null)
        {
            if (!string.IsNullOrEmpty(agent.Instructions))
                messages.Insert(0, new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, agent.Instructions));

            tools = await agentHelper.GetAgentToolsAsync(agent, cancellationToken);
            chatOptions = new ChatOptions { Tools = [.. tools.Tools] };
        }

        return new ChatTurn(messages, chatOptions, tools);
    }

    private sealed record ChatTurn(
        List<Microsoft.Extensions.AI.ChatMessage> Messages,
        ChatOptions? Options,
        AgentToolSet Tools) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Tools.DisposeAsync();
    }
}
