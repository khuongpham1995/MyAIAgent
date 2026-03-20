namespace MyAIAgent.McpServer.Domain.Interfaces;

public interface IApiClient<T> where T : class
{
    Task<List<T>> GetAsync(string action, CancellationToken ct);
    Task<T?> GetByIdAsync(string action, int id, CancellationToken ct);
    Task<T> CreateAsync(string action, T newEntity, CancellationToken ct);
    Task<T> UpdateAsync(string action, int id, T updatedEntity, CancellationToken ct);
    Task DeleteAsync(string action, int id, CancellationToken ct);
}