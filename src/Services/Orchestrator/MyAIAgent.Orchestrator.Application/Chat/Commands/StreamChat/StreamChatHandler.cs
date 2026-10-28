/**
 * MyAIAgent | Orchestrator · Application
 *
 * Coordinates streamed replies, session events, committed turns, and optional title/summary updates.
 */

using System.Runtime.CompilerServices;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using MyAIAgent.Orchestrator.Application.Common.Exceptions;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Application.Chat.Commands.StreamChat;

public sealed class StreamChatHandler(
    IChatOrchestratorService orchestratorService,
    IChatSessionRepository sessionRepository,
    IUnitOfWork unitOfWork,
    IConversationTitleGenerator titleGenerator,
    IConversationSummarizer summarizer,
    TimeProvider timeProvider,
    ILogger<StreamChatHandler> logger)
    : IStreamRequestHandler<StreamChatCommand, ChatStreamEvent>
{
    public async IAsyncEnumerable<ChatStreamEvent> Handle(
        StreamChatCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatSession? session = null;

        // A session id that is unknown to this user, including one owned by someone else, starts a new conversation.
        if (!string.IsNullOrEmpty(command.SessionId))
            session = await sessionRepository.FindForUserAsync(command.SessionId, command.UserId, cancellationToken);

        var isNewSession = session is null;
        session ??= new ChatSession
        {
            SessionId = Guid.NewGuid().ToString(),
            UserId = command.UserId,
            Title = command.UserMessage[..Math.Min(100, command.UserMessage.Length)],
            AgentId = command.AgentId,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        yield return ChatStreamEvent.Session(session.SessionId);

        var request = new ChatRequest(
            command.UserMessage,
            ChatSessionHistory.Build(session),
            command.AgentId ?? session.AgentId);

        var reply = new StringBuilder();
        await using var enumerator = orchestratorService
            .StreamChatAsync(request, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        // C# forbids yield inside a catch clause, so a failure is recorded and reported afterwards.
        string? failureMessage = null;
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or AiProviderException)
            {
                logger.LogError(ex, "AI provider streaming request failed for session {SessionId}", session.SessionId);
                failureMessage = "AI service temporarily unavailable.";
                break;
            }
            catch (AgentNotFoundException ex)
            {
                logger.LogError(ex, "Configured agent was not found for session {SessionId}", session.SessionId);
                failureMessage = "Agent not found.";
                break;
            }

            if (!hasNext)
                break;

            reply.Append(enumerator.Current);
            yield return ChatStreamEvent.Delta(enumerator.Current);
        }

        if (failureMessage is not null)
        {
            yield return ChatStreamEvent.Error(failureMessage);
            yield break;
        }

        var replyText = reply.ToString();
        if (string.IsNullOrWhiteSpace(replyText))
        {
            yield return ChatStreamEvent.Error("The assistant returned an empty response.");
            yield break;
        }

        ChatSessionHistory.AppendTurn(session, command.UserMessage, replyText, timeProvider.GetUtcNow());

        if (isNewSession)
            await sessionRepository.AddAsync(session, cancellationToken);

        // The turn is committed before any slow best-effort call so a disconnect or provider hiccup cannot lose it.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (isNewSession)
        {
            var generatedTitle = await ChatSessionHistory.RunBestEffortAsync(
                token => titleGenerator.GenerateTitleAsync(command.UserMessage, replyText, token),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(generatedTitle) && generatedTitle != session.Title)
            {
                var previousTitle = session.Title;
                session.Title = generatedTitle;
                if (!await ChatSessionHistory.TrySaveAsync(unitOfWork, logger, "conversation title", cancellationToken))
                    session.Title = previousTitle;
            }

            yield return ChatStreamEvent.TitleGenerated(session.Title);
        }

        // The turn is already saved, so a failed summary save must not turn a successful turn into an error;
        // the summary is retried on a later turn because the thresholds are unchanged.
        var previousSummary = session.Summary;
        var previousSummarizedCount = session.SummarizedMessageCount;
        if (await ChatSessionHistory.TrySummarizeAsync(session, summarizer, cancellationToken)
            && !await ChatSessionHistory.TrySaveAsync(unitOfWork, logger, "conversation summary", cancellationToken))
        {
            session.Summary = previousSummary;
            session.SummarizedMessageCount = previousSummarizedCount;
        }

        yield return ChatStreamEvent.Done(session.SessionId);
    }
}
