/**
 * MyAIAgent | Gateway · Application
 *
 * Forwards owner-scoped conversation deletion with trusted owner identity to Orchestrator.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;
using MyAIAgent.Gateway.Application.Common.Interfaces;

namespace MyAIAgent.Gateway.Application.Conversations.Commands.DeleteConversation;

public sealed class DeleteConversationHandler(IOrchestratorClient orchestratorClient)
    : IRequestHandler<DeleteConversationCommand, ResponseModel<bool>>
{
    public async Task<ResponseModel<bool>> Handle(
        DeleteConversationCommand command,
        CancellationToken cancellationToken) =>
        await orchestratorClient.DeleteConversationAsync(command.SessionId, command.UserId, cancellationToken);
}
