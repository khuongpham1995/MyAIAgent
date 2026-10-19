using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MyAIAgent.Common.Domain.Abstractions;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Infrastructure.Services;

public sealed class ConversationTitleGenerator(
    IChatClient chatClient,
    ILogger<ConversationTitleGenerator> logger)
    : IConversationTitleGenerator, IScoped
{
    private const int MaxTitleLength = 100;
    private const int MaxWords = 5;

    private const string Prompt =
        "Write a title of at most 5 words for the conversation below. " +
        "Reply with the title only: no quotes, no trailing punctuation.";

    public async Task<string> GenerateTitleAsync(string userMessage, string reply, CancellationToken cancellationToken)
    {
        var fallback = Truncate(userMessage);

        try
        {
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, Prompt),
                    new ChatMessage(ChatRole.User, $"User: {Truncate(userMessage, 500)}\nAssistant: {Truncate(reply, 500)}"),
                ],
                cancellationToken: cancellationToken);

            var title = Normalize(response.Text);
            return string.IsNullOrWhiteSpace(title) ? fallback : title;
        }
        catch (Exception ex) when (ex is HttpRequestException or ClientResultException)
        {
            logger.LogWarning(ex, "Conversation title generation failed; using the first message instead");
            return fallback;
        }
    }

    private static string Normalize(string? raw)
    {
        var words = (raw ?? string.Empty)
            .Replace('\n', ' ')
            .Trim()
            .Trim('"', '\'', '.', '!', '?', ':', ';', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(MaxWords);

        return Truncate(string.Join(' ', words));
    }

    private static string Truncate(string value, int length = MaxTitleLength) =>
        value.Length <= length ? value : value[..length];
}
