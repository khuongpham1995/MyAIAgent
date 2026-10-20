/**
 * MyAIAgent | Orchestrator · Application
 *
 * Deletes an existing agent and commits the change, or returns a missing-agent result.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Agents.Commands.DeleteAgent;

public sealed class DeleteAgentHandler(IAgentRepository agentRepository, IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteAgentCommand, ResponseModel<bool>>
{
    public async Task<ResponseModel<bool>> Handle(DeleteAgentCommand command, CancellationToken cancellationToken)
    {
        var existing = await agentRepository.FindByIdAsync(command.AgentId, cancellationToken);
        if (existing is null)
            return ResponseModel<bool>.Fail(message: "Agent not found.");

        agentRepository.Delete(existing);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ResponseModel<bool>.Succeed(true);
    }
}
