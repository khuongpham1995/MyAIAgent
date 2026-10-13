using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Application.Interfaces;
using MyAIAgent.Orchestrator.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Handlers.Chat;

public sealed class ChatHandler(
    IChatOrchestratorService orchestratorService,
    IChatSessionRepository sessionRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<ChatCommand, ResponseModel<ChatResponse>>
{
    public async Task<ResponseModel<ChatResponse>> Handle(ChatCommand command, CancellationToken cancellationToken)
    {
        var isExistingSession = !string.IsNullOrEmpty(command.SessionId);
        var now = timeProvider.GetUtcNow();

        var session = isExistingSession
            ? await sessionRepository.FindBySessionIdAsync(command.SessionId!, cancellationToken)
            : null;

        if (session is not null && session.UserId != command.UserId)
            return ResponseModel<ChatResponse>.Fail(message: "Chat session not found.");

        var isNewSession = session is null;
        session ??= new ChatSession
        {
            SessionId = Guid.NewGuid().ToString(),
            UserId = command.UserId,
            Title = command.UserMessage[..Math.Min(100, command.UserMessage.Length)],
            CreatedAt = now
        };

        var history = session.Messages
            .Select(m => new ChatMessage(m.Role, m.Content))
            .ToList();

        var request = new ChatRequest(command.UserMessage, history, command.AgentId);
        var aiResult = await orchestratorService.ChatAsync(request, cancellationToken);

        if (!aiResult.IsSuccess)
            return ResponseModel<ChatResponse>.Fail(message: aiResult.Message);

        var reply = aiResult.Data!;

        session.Messages.Add(new ChatSessionMessage { Role = "user", Content = command.UserMessage, CreatedAt = now });
        session.Messages.Add(new ChatSessionMessage { Role = "assistant", Content = reply, CreatedAt = now });

        if (isNewSession)
            await sessionRepository.AddAsync(session, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ResponseModel<ChatResponse>.Succeed(new ChatResponse(reply, session.SessionId));
    }
}
