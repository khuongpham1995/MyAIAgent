using Mapster;
using MyAIAgent.MainSubSys.Application.Agents.Commands.CreateAgent;
using MyAIAgent.MainSubSys.Application.Agents;

namespace MyAIAgent.MainSubSys.Application.Common.Mappings;

public sealed class CreateAgentMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CreateAgentRequestDto, CreateAgentCommand>()
            .MapWith(source => new CreateAgentCommand(
                source.Name,
                source.Instructions,
                source.McpServerEndpoint,
                source.IsDefault,
                source.EnabledTools));
    }
}
