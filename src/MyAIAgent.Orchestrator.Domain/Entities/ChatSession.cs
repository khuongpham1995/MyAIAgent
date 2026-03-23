namespace MyAIAgent.Orchestrator.Domain.Entities;

public sealed class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public IList<ChatSessionMessage> Messages { get; set; } = [];
}
