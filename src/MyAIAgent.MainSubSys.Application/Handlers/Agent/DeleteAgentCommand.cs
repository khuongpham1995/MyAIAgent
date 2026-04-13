using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Domain.Interfaces;

namespace MyAIAgent.MainSubSys.Application.Handlers.Agent;

public sealed record DeleteAgentCommand(string AgentId) : IRequest<ResponseModel<bool>>;

public sealed class DeleteAgentHandler(IOrchestratorClient orchestratorClient)
    : IRequestHandler<DeleteAgentCommand, ResponseModel<bool>>
{
    public async Task<ResponseModel<bool>> Handle(
        DeleteAgentCommand command,
        CancellationToken cancellationToken) =>
        await orchestratorClient.DeleteAgentAsync(command.AgentId, cancellationToken);
}

public sealed class DeleteAgentCommandValidator : AbstractValidator<DeleteAgentCommand>
{
    public DeleteAgentCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty().WithMessage("Agent ID is required.");
    }
}
