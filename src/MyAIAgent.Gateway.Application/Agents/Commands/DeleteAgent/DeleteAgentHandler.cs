using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Application.Common.Interfaces;

namespace MyAIAgent.Gateway.Application.Agents.Commands.DeleteAgent;

public sealed class DeleteAgentHandler(IOrchestratorClient orchestratorClient)
    : IRequestHandler<DeleteAgentCommand, ResponseModel<bool>>
{
    public async Task<ResponseModel<bool>> Handle(
        DeleteAgentCommand command,
        CancellationToken cancellationToken) =>
        await orchestratorClient.DeleteAgentAsync(command.AgentId, cancellationToken);
}
