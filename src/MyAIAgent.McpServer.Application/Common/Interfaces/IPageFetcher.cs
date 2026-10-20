/**
 * MyAIAgent | McpServer · Application
 *
 * Defines the web-search application port for retrieving readable content from a result URL.
 */

using MyAIAgent.McpServer.Application.Search;

namespace MyAIAgent.McpServer.Application.Common.Interfaces;

public interface IPageFetcher
{
    Task<FetchResult> GetContentAsync(string url, CancellationToken cancellationToken = default);
}
