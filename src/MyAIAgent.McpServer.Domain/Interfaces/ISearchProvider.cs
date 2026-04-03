using MyAIAgent.McpServer.Domain.Models;

namespace MyAIAgent.McpServer.Domain.Interfaces;

public interface ISearchProvider
{
    IAsyncEnumerable<SearchEntry> SearchAsync(string query, int top, CancellationToken cancellationToken = default);
}
