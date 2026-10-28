/**
 * MyAIAgent | Common · Domain
 *
 * Provides a shared generic identifier for domain entities without coupling them to persistence.
 */

namespace MyAIAgent.Common.Domain.Abstractions;

public abstract class BaseEntity<TKey>
{
    protected BaseEntity(TKey id) => Id = id;

    public TKey Id { get; set; }
}
