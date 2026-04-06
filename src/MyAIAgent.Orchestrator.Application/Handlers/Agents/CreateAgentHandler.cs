using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Domain.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Handlers.Agents;

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
