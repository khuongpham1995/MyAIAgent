/**
 * MyAIAgent | Orchestrator · Application
 *
 * Validates AgentId before agent deletion reaches the Orchestrator handler.
 */

using FluentValidation;

namespace MyAIAgent.Orchestrator.Application.Agents.Commands.DeleteAgent;

public sealed class DeleteAgentCommandValidator : AbstractValidator<DeleteAgentCommand>
{
    public DeleteAgentCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty().WithMessage("Agent ID is required.");
    }
}
