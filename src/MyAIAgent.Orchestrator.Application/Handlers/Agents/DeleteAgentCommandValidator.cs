using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Application.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Handlers.Agents;

public sealed class DeleteAgentCommandValidator : AbstractValidator<DeleteAgentCommand>
{
    public DeleteAgentCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty().WithMessage("Agent ID is required.");
    }
}
