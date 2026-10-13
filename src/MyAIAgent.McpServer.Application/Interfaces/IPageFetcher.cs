using MyAIAgent.McpServer.Domain.Models;

namespace MyAIAgent.McpServer.Application.Interfaces;

public interface IPageFetcher
{
    Task<FetchResult> GetContentAsync(string url, CancellationToken cancellationToken = default);
}
