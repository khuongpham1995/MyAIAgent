using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Domain.Interfaces;

namespace MyAIAgent.MainSubSys.Application.Handlers.Agent;

public sealed record CreateAgentCommand(
    string Name,
    string Instructions,
    string? McpServerEndpoint,
    bool IsDefault,
    IReadOnlyList<string>? EnabledTools = null)
    : IRequest<ResponseModel<string>>;

public sealed class CreateAgentHandler(IOrchestratorClient orchestratorClient)
    : IRequestHandler<CreateAgentCommand, ResponseModel<string>>
{
    public async Task<ResponseModel<string>> Handle(
        CreateAgentCommand command,
        CancellationToken cancellationToken) =>
        await orchestratorClient.CreateAgentAsync(
            command.Name,
            command.Instructions,
            command.McpServerEndpoint,
            command.IsDefault,
            command.EnabledTools,
            cancellationToken);
}

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
            .Must(v => Uri.TryCreate(v, UriKind.Absolute, out _))
            .WithMessage("MCP server endpoint must be a valid absolute URI when provided.")
            .When(x => !string.IsNullOrEmpty(x.McpServerEndpoint));
    }
}
