using MyAIAgent.MainSubSys.Application.Agents.Commands.CreateAgent;
using MyAIAgent.MainSubSys.Application.Agents.Commands.DeleteAgent;
using MyAIAgent.MainSubSys.Application.Agents.Queries.ListAgents;
using MyAIAgent.MainSubSys.Application.Models;

namespace MyAIAgent.MainSubSys.Application.Mappers;

public static class CreateAgentCommandMapper
{
    public static CreateAgentCommand ToCommand(this CreateAgentRequestDto dto) =>
        new(dto.Name, dto.Instructions, dto.McpServerEndpoint, dto.IsDefault, dto.EnabledTools);
}
