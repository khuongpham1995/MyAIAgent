namespace MyAIAgent.Orchestrator.Domain.Entities;

public sealed class AgentDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string McpServerEndpoint { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    // IList required by EF Core Cosmos OwnsMany for change tracking — do not change to IReadOnlyList
    public IList<AgentTool> AgentTools { get; set; } = [];
}
