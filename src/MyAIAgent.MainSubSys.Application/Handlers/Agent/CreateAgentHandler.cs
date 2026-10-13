using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Interfaces;

namespace MyAIAgent.MainSubSys.Application.Handlers.Agent;

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
