/**
 * MyAIAgent | Gateway · Application
 *
 * Validates AgentId before agent deletion reaches the Gateway handler.
 */

using FluentValidation;

namespace MyAIAgent.Gateway.Application.Agents.Commands.DeleteAgent;

public sealed class DeleteAgentCommandValidator : AbstractValidator<DeleteAgentCommand>
{
    public DeleteAgentCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty().WithMessage("Agent ID is required.");
    }
}
