using MyAIAgent.Common.Domain.Abstractions;

namespace MyAIAgent.Orchestrator.Domain.Entities;

public sealed class ChatSession : BaseEntity<string>
{
    public ChatSession() : base(Guid.NewGuid().ToString()) { }

    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? AgentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? Summary { get; set; }
    public int SummarizedMessageCount { get; set; }
    public IList<ChatSessionMessage> Messages { get; set; } = [];
}
