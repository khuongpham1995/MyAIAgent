using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MyAIAgent.McpServer.Application.Interfaces;
using MyAIAgent.McpServer.Domain.Models;

namespace MyAIAgent.McpServer.Application.Search.Queries.WebSearch;

public sealed class WebSearchHandler(
    ISearchProvider searchProvider,
    IPageFetcher pageFetcher,
    ILogger<WebSearchHandler> logger)
    : IRequestHandler<WebSearchQuery, string>
{
    public async Task<string> Handle(WebSearchQuery request, CancellationToken cancellationToken)
    {
        var entries = new List<SearchEntry>();
        await foreach (var item in searchProvider.SearchAsync(request.Query, request.Top, cancellationToken))
            entries.Add(item);

        var fetchTasks = entries
            .Where(e => !string.IsNullOrEmpty(e.Link))
            .Select(async e =>
            {
                try
                {
                    var page = await pageFetcher.GetContentAsync(e.Link!, cancellationToken);
                    if (!page.Success) return null;
                    return new PageContent(e.Link!, page.Content ?? string.Empty);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(ex, "Failed to fetch {Url} — skipping", e.Link);
                    return null;
                }
            });

        var results = (await Task.WhenAll(fetchTasks))
            .Where(r => r is not null)
            .Cast<PageContent>()
            .ToList();

        return JsonSerializer.Serialize(results);
    }
}
