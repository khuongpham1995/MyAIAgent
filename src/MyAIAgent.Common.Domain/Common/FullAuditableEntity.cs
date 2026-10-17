namespace MyAIAgent.Common.Domain.Common;

public abstract class FullAuditableEntity<TKey>(TKey id) : AuditableEntity<TKey>(id)
{
    public DateTimeOffset? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}
