/**
 * MyAIAgent | Orchestrator · Application
 *
 * Validates Name, Instructions, McpServerEndpoint before agent creation reaches the Orchestrator handler.
 */

using FluentValidation;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Agents.Commands.CreateAgent;

public sealed class CreateAgentCommandValidator : AbstractValidator<CreateAgentCommand>
{
    public CreateAgentCommandValidator(IMcpEndpointPolicy endpointPolicy)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Agent name is required.")
            .MaximumLength(200).WithMessage("Agent name must not exceed 200 characters.");
        RuleFor(x => x.Instructions)
            .NotEmpty().WithMessage("Agent instructions are required.");
        RuleFor(x => x.McpServerEndpoint)
            .Must(endpointPolicy.IsAllowed)
            .WithMessage("MCP server endpoint must use the configured service or an approved public HTTPS origin.");
    }
}
