namespace MyAIAgent.Common.Domain.Abstractions;

public abstract class AuditableEntity<TKey>(TKey id) : BaseEntity<TKey>(id)
{
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
