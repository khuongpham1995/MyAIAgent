using MyAIAgent.Orchestrator.Application.Agents.Commands.CreateAgent;
using MyAIAgent.Orchestrator.Application.Agents.Commands.DeleteAgent;
using MyAIAgent.Orchestrator.Application.Agents.Queries.ListAgents;
using MyAIAgent.Orchestrator.Application.Models;

namespace MyAIAgent.Orchestrator.Application.Mappers;

public static class AgentCommandMapper
{
    public static CreateAgentCommand ToCommand(this CreateAgentRequestDto dto)
        => new(dto.Name, dto.Instructions, dto.McpServerEndpoint, dto.IsDefault, dto.EnabledTools);
}
