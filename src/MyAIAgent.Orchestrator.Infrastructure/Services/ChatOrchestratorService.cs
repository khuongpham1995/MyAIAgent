using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Common.Domain.Common;
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

            var agent = request.AgentId is not null
                ? await agentRepository.FindByIdAsync(request.AgentId, cancellationToken)
                : await agentRepository.FindDefaultAsync(cancellationToken);

            if (agent is not null)
            {
                if (!string.IsNullOrEmpty(agent.Instructions))
                    messages.Insert(0, new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, agent.Instructions));

                var tools = await agentHelper.GetAgentToolsAsync(agent, cancellationToken);
                chatOptions = new ChatOptions { Tools = [.. tools] };
            }

            var completion = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
            return ResponseModel<string>.Succeed(completion.Text);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "AI provider request failed");
            return ResponseModel<string>.Fail(message: "AI service temporarily unavailable.");
        }
    }
}
