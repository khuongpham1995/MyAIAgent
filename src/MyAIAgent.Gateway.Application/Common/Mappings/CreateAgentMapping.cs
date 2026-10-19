using Mapster;
using MyAIAgent.Gateway.Application.Agents.Commands.CreateAgent;
using MyAIAgent.Gateway.Application.Agents;

namespace MyAIAgent.Gateway.Application.Common.Mappings;

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
