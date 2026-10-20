/**
 * MyAIAgent | Orchestrator · Application
 *
 * Validates Name, Instructions, McpServerEndpoint before agent creation reaches the Orchestrator handler.
 */

using FluentValidation;

namespace MyAIAgent.Orchestrator.Application.Agents.Commands.CreateAgent;

public sealed class CreateAgentCommandValidator : AbstractValidator<CreateAgentCommand>
{
    public CreateAgentCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Agent name is required.")
            .MaximumLength(200).WithMessage("Agent name must not exceed 200 characters.");
        RuleFor(x => x.Instructions)
            .NotEmpty().WithMessage("Agent instructions are required.");
        RuleFor(x => x.McpServerEndpoint)
            .Must(v => string.IsNullOrEmpty(v) || Uri.TryCreate(v, UriKind.Absolute, out _))
            .WithMessage("MCP server endpoint must be a valid absolute URI when provided.");
    }
}
