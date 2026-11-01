/**
 * MyAIAgent | Orchestrator · Application
 *
 * Projects agent definitions and enabled tools into Orchestrator application responses.
 */

using Mapster;
using MyAIAgent.Orchestrator.Application.Agents.Commands.CreateAgent;
using MyAIAgent.Orchestrator.Application.Agents;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Application.Common.Mappings;

public sealed class AgentMapping : IRegister
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

        config.NewConfig<AgentDefinition, AgentSummary>()
            .MapWith(source => new AgentSummary(
                source.Id,
                source.Name,
                source.Instructions,
                source.McpServerEndpoint,
                source.IsDefault,
                source.AgentTools
                    .Where(tool => tool.IsEnabled)
                    .Select(tool => new McpToolInfo(tool.Name, null))
                    .ToList()));
    }
}
