using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Common.Domain.ServiceLifetime;
using MyAIAgent.Orchestrator.Domain.Interfaces;
using MyAIAgent.Orchestrator.Domain.Models;

namespace MyAIAgent.Orchestrator.Infrastructure.Services;

public sealed class ChatOrchestratorService(IChatClient chatClient, ILogger<ChatOrchestratorService> logger)
    : IChatOrchestratorService, IScoped
{
    public async Task<ResponseModel<string>> ChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var messages = request.History
                .Select(m => new Microsoft.Extensions.AI.ChatMessage(
                    m.Role == "assistant" ? ChatRole.Assistant : ChatRole.User,
                    m.Content))
                .ToList();
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, request.UserMessage));

            var completion = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            return ResponseModel<string>.Succeed(completion.Text);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "AI provider request failed");
            return ResponseModel<string>.Fail(message: "AI service temporarily unavailable.");
        }
    }
}
