/**
 * MyAIAgent | Orchestrator · Domain
 *
 * Stores an agent-owned tool name and enablement flag.
 */

using MyAIAgent.Common.Domain.Abstractions;

namespace MyAIAgent.Orchestrator.Domain.Entities;

public sealed class AgentTool : BaseEntity<string>
{
    public AgentTool() : base(Guid.NewGuid().ToString()) { }

    public string AgentDefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
