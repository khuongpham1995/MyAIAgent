namespace MyAIAgent.Orchestrator.Domain.Entities;

public sealed class AgentTool
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AgentDefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
