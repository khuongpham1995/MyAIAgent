using MyAIAgent.McpServer.Domain.Models;

namespace MyAIAgent.McpServer.Domain.Interfaces;

public interface IPageFetcher
{
    Task<FetchResult> GetContentAsync(string url, CancellationToken cancellationToken = default);
}
