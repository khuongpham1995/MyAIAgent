using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Domain.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Handlers.Agents;

public sealed record CreateAgentCommand(
    string Name,
    string Instructions,
    string McpServerEndpoint,
    bool IsDefault,
    IReadOnlyList<string>? EnabledTools = null)
    : IRequest<ResponseModel<string>>;

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

public sealed class CreateAgentHandler(IAgentRepository agentRepository)
    : IRequestHandler<CreateAgentCommand, ResponseModel<string>>
{
    public async Task<ResponseModel<string>> Handle(CreateAgentCommand command, CancellationToken cancellationToken)
    {
        var agentId = Guid.NewGuid().ToString();
        var agent = new AgentDefinition
        {
            Id = agentId,
            Name = command.Name,
            Instructions = command.Instructions,
            McpServerEndpoint = command.McpServerEndpoint,
            IsDefault = command.IsDefault,
            AgentTools = [.. (command.EnabledTools ?? [])
                .Select(name => new AgentTool { AgentDefinitionId = agentId, Name = name, IsEnabled = true })],
        };

        await agentRepository.AddAsync(agent, cancellationToken);
        return ResponseModel<string>.Succeed(agent.Id);
    }
}
