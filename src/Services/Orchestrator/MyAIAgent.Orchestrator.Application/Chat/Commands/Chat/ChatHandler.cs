/**
 * MyAIAgent | Orchestrator · Application
 *
 * Coordinates session ownership, model replies, turn persistence, and best-effort summary updates.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Chat.Commands.Chat;

public sealed class ChatHandler(
    IChatOrchestratorService orchestratorService,
    IChatSessionRepository sessionRepository,
    IUnitOfWork unitOfWork,
    IConversationSummarizer summarizer,
    TimeProvider timeProvider,
    ILogger<ChatHandler> logger)
    : IRequestHandler<ChatCommand, ResponseModel<ChatResponse>>
{
    public async Task<ResponseModel<ChatResponse>> Handle(ChatCommand command, CancellationToken cancellationToken)
    {
        var isExistingSession = !string.IsNullOrEmpty(command.SessionId);
        var now = timeProvider.GetUtcNow();

        // A session id that is unknown to this user, including one owned by someone else, starts a new conversation.
        var session = isExistingSession
            ? await sessionRepository.FindForUserAsync(command.SessionId!, command.UserId, cancellationToken)
            : null;

        var isNewSession = session is null;
        session ??= new ChatSession
        {
            SessionId = Guid.NewGuid().ToString(),
            UserId = command.UserId,
            Title = command.UserMessage[..Math.Min(100, command.UserMessage.Length)],
            AgentId = command.AgentId,
            CreatedAt = now
        };

        var request = new ChatRequest(
            command.UserMessage,
            ChatSessionHistory.Build(session),
            command.AgentId ?? session.AgentId);
        var aiResult = await orchestratorService.ChatAsync(request, cancellationToken);

        if (!aiResult.IsSuccess)
            return ResponseModel<ChatResponse>.Fail(message: aiResult.Message);

        var reply = aiResult.Data!;

        ChatSessionHistory.AppendTurn(session, command.UserMessage, reply, now);

        if (isNewSession)
            await sessionRepository.AddAsync(session, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The reply is already saved, so a failed summary save must not turn a successful turn into an error.
        var previousSummary = session.Summary;
        var previousSummarizedCount = session.SummarizedMessageCount;
        if (await ChatSessionHistory.TrySummarizeAsync(session, summarizer, cancellationToken)
            && !await ChatSessionHistory.TrySaveAsync(unitOfWork, logger, "conversation summary", cancellationToken))
        {
            session.Summary = previousSummary;
            session.SummarizedMessageCount = previousSummarizedCount;
        }

        return ResponseModel<ChatResponse>.Succeed(new ChatResponse(reply, session.SessionId));
    }
}
