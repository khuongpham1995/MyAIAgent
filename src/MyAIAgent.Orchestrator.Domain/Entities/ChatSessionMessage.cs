using MyAIAgent.Common.Domain.Entities;

namespace MyAIAgent.Orchestrator.Domain.Entities;

public sealed class ChatSessionMessage : BaseEntity<string>
{
    public ChatSessionMessage() : base(Guid.NewGuid().ToString()) { }

    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
