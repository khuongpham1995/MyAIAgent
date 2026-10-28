/**
 * MyAIAgent | Orchestrator · Application
 *
 * Creates and commits an agent definition with its configured tool entries.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Agents.Commands.CreateAgent;

public sealed class CreateAgentHandler(IAgentRepository agentRepository, IUnitOfWork unitOfWork)
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
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ResponseModel<string>.Succeed(agent.Id);
    }
}
