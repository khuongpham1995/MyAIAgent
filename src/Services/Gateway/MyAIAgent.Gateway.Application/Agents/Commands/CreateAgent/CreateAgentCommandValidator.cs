/**
 * MyAIAgent | Gateway · Application
 *
 * Validates Name, Instructions, McpServerEndpoint before agent creation reaches the Gateway handler.
 */

using FluentValidation;

namespace MyAIAgent.Gateway.Application.Agents.Commands.CreateAgent;

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
            .Must(v => Uri.TryCreate(v, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0)
            .WithMessage("MCP server endpoint must be an HTTP or HTTPS URI without credentials or a fragment when provided.")
            .When(x => !string.IsNullOrEmpty(x.McpServerEndpoint));
    }
}
