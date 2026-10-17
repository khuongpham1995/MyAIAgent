namespace MyAIAgent.Common.Domain.Entities;

public abstract class BaseEntity<TKey>
{
    protected BaseEntity(TKey id) => Id = id;

    public TKey Id { get; set; }
}
