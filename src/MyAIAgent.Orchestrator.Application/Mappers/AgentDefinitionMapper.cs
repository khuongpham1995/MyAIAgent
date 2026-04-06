using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Mappers;

public static class AgentDefinitionMapper
{
    public static AgentSummary ToSummary(this AgentDefinition agent)
        => new(agent.Id, agent.Name, agent.Instructions, agent.McpServerEndpoint, agent.IsDefault,
            [.. agent.AgentTools.Where(t => t.IsEnabled).Select(t => new McpToolInfo(t.Name, null))]);
}
