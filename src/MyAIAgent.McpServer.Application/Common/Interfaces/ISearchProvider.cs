/**
 * MyAIAgent | McpServer · Application
 *
 * Defines the asynchronous search-results port used before web-search page retrieval.
 */

using MyAIAgent.McpServer.Application.Search;

namespace MyAIAgent.McpServer.Application.Common.Interfaces;

public interface ISearchProvider
{
    IAsyncEnumerable<SearchEntry> SearchAsync(string query, int top, CancellationToken cancellationToken = default);
}
