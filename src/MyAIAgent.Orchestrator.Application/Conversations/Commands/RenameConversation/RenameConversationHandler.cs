/**
 * MyAIAgent | Orchestrator · Application
 *
 * Checks ownership before updating and committing a conversation title.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Conversations.Commands.RenameConversation;

public sealed class RenameConversationHandler(
    IChatSessionRepository sessionRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<RenameConversationCommand, ResponseModel<bool>>
{
    public async Task<ResponseModel<bool>> Handle(
        RenameConversationCommand command,
        CancellationToken cancellationToken)
    {
        var session = await sessionRepository.FindForUserAsync(command.SessionId, command.UserId, cancellationToken);

        if (session is null || session.UserId != command.UserId)
            return ResponseModel<bool>.Fail(message: "Conversation not found.");

        session.Title = command.Title.Trim();
        session.UpdatedAt = timeProvider.GetUtcNow();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ResponseModel<bool>.Succeed(true);
    }
}
