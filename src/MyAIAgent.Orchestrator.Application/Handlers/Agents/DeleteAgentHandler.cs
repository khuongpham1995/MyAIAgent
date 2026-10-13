using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Application.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Handlers.Agents;

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
