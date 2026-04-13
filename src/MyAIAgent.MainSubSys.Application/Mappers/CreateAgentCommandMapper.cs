using MyAIAgent.MainSubSys.Application.Handlers.Agent;
using MyAIAgent.MainSubSys.Application.Models;

namespace MyAIAgent.MainSubSys.Application.Mappers;

public static class CreateAgentCommandMapper
{
    public static CreateAgentCommand ToCommand(this CreateAgentRequestDto dto) =>
        new(dto.Name, dto.Instructions, dto.McpServerEndpoint, dto.IsDefault, dto.EnabledTools);
}
