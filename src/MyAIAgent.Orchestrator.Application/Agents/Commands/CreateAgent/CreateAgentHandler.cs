using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Application.Interfaces;

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
