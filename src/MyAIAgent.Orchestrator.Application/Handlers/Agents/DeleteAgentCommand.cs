using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;

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
