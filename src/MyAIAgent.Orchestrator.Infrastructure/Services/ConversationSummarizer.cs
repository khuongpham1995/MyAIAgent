using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MyAIAgent.Common.Domain.Abstractions;
using MyAIAgent.Orchestrator.Application.Chat;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Infrastructure.Services;

public sealed class ConversationSummarizer(
    IChatClient chatClient,
    ILogger<ConversationSummarizer> logger)
    : IConversationSummarizer, IScoped
{
    private const string Prompt =
        "You maintain a running summary of a conversation between a user and an assistant. " +
        "Merge the previous summary with the new messages into one concise summary. " +
        "Keep facts, decisions, names and open questions. Reply with the summary only.";

    public async Task<string?> SummarizeAsync(
        string? previousSummary,
        IReadOnlyList<MyAIAgent.Orchestrator.Application.Chat.ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        var transcript = string.Join('\n', messages.Select(m => $"{m.Role}: {m.Content}"));
        var input = string.IsNullOrWhiteSpace(previousSummary)
            ? transcript
            : $"Previous summary:\n{previousSummary}\n\nNew messages:\n{transcript}";

        try
        {
            var response = await chatClient.GetResponseAsync(
                [
                    new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, Prompt),
                    new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, input),
                ],
                cancellationToken: cancellationToken);

            return response.Text?.Trim();
        }
        catch (Exception ex) when (ex is HttpRequestException or ClientResultException)
        {
            logger.LogWarning(ex, "Conversation summarization failed; it will be retried on the next turn");
            return null;
        }
    }
}
