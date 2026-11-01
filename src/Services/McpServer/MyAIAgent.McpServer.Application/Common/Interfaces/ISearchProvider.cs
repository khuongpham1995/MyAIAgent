/**
 * MyAIAgent | McpServer · Application
 *
 * Provides ranked search excerpts or an explicit provider failure.
 */

using MyAIAgent.Common.Application.Models;
using MyAIAgent.McpServer.Application.Search;

namespace MyAIAgent.McpServer.Application.Common.Interfaces;

public interface ISearchProvider
{
    Task<ResponseModel<IReadOnlyList<SearchEntry>>> SearchAsync(string query, int top, CancellationToken cancellationToken = default);
}
