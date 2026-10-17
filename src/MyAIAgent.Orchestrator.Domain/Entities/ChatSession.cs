using MyAIAgent.Common.Domain.Entities;

namespace MyAIAgent.Orchestrator.Domain.Entities;

public sealed class ChatSession : BaseEntity<string>
{
    public ChatSession() : base(Guid.NewGuid().ToString()) { }

    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public IList<ChatSessionMessage> Messages { get; set; } = [];
}
