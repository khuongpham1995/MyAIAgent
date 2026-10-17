using MyAIAgent.McpServer.Application.Search;

namespace MyAIAgent.McpServer.Application.Common.Interfaces;

public interface IPageFetcher
{
    Task<FetchResult> GetContentAsync(string url, CancellationToken cancellationToken = default);
}
