/**
 * MyAIAgent | Orchestrator · Application
 *
 * Checks ownership before deleting and committing a conversation.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Conversations.Commands.DeleteConversation;

public sealed class DeleteConversationHandler(
    IChatSessionRepository sessionRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteConversationCommand, ResponseModel<bool>>
{
    public async Task<ResponseModel<bool>> Handle(
        DeleteConversationCommand command,
        CancellationToken cancellationToken)
    {
        var session = await sessionRepository.FindForUserAsync(command.SessionId, command.UserId, cancellationToken);

        if (session is null || session.UserId != command.UserId)
            return ResponseModel<bool>.Fail(message: "Conversation not found.");

        sessionRepository.Delete(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ResponseModel<bool>.Succeed(true);
    }
}
