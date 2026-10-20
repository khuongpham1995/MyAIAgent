/**
 * MyAIAgent | Gateway · Application
 *
 * Forwards owner-scoped conversation renaming with trusted owner identity to Orchestrator.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Application.Common.Interfaces;

namespace MyAIAgent.Gateway.Application.Conversations.Commands.RenameConversation;

public sealed class RenameConversationHandler(IOrchestratorClient orchestratorClient)
    : IRequestHandler<RenameConversationCommand, ResponseModel<bool>>
{
    public async Task<ResponseModel<bool>> Handle(
        RenameConversationCommand command,
        CancellationToken cancellationToken) =>
        await orchestratorClient.RenameConversationAsync(
            command.SessionId, command.UserId, command.Title, cancellationToken);
}
