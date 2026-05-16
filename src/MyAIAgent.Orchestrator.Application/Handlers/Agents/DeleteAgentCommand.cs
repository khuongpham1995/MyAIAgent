using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Domain.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Handlers.Agents;

public sealed record DeleteAgentCommand(string AgentId) : IRequest<ResponseModel<bool>>;

public sealed class DeleteAgentCommandValidator : AbstractValidator<DeleteAgentCommand>
{
    public DeleteAgentCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty().WithMessage("Agent ID is required.");
    }
}

public sealed class DeleteAgentHandler(IAgentRepository agentRepository)
    : IRequestHandler<DeleteAgentCommand, ResponseModel<bool>>
{
    public async Task<ResponseModel<bool>> Handle(DeleteAgentCommand command, CancellationToken cancellationToken)
    {
        var existing = await agentRepository.FindByIdAsync(command.AgentId, cancellationToken);
        if (existing is null)
            return ResponseModel<bool>.Fail(message: "Agent not found.");

        await agentRepository.DeleteAsync(existing, cancellationToken);
        return ResponseModel<bool>.Succeed(true);
    }
}
