/**
 * MyAIAgent | Gateway · Application
 *
 * Forwards agent deletion to Orchestrator through the Gateway client port.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;
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
