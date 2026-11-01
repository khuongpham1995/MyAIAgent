/**
 * MyAIAgent | McpServer · Application
 *
 * Bounds and deduplicates ranked search evidence without retrieving source pages.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;
using MyAIAgent.McpServer.Application.Common.Interfaces;

namespace MyAIAgent.McpServer.Application.Search.Queries.WebSearch;

public sealed class WebSearchHandler(ISearchProvider searchProvider, TimeProvider timeProvider)
    : IRequestHandler<WebSearchQuery, ResponseModel<IReadOnlyList<PageContent>>>
{
    public async Task<ResponseModel<IReadOnlyList<PageContent>>> Handle(WebSearchQuery request, CancellationToken cancellationToken)
    {
        var response = await searchProvider.SearchAsync(request.Query, request.Top, cancellationToken);
        if (!response.IsSuccess)
            return ResponseModel<IReadOnlyList<PageContent>>.Fail(message: response.Message);

        var results = new List<PageContent>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var remaining = 20_000;
        var retrievedAt = timeProvider.GetUtcNow();
        foreach (var entry in response.Data ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (results.Count >= request.Top || remaining == 0) break;
            if (entry.Url is null || entry.Url.Length > 2048
                || !Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https") || !uri.IsDefaultPort
                || !string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrEmpty(uri.Host)
                || !seen.Add(uri.AbsoluteUri)) continue;

            var content = Limit(entry.Content ?? string.Empty, Math.Min(3000, remaining));
            var title = Limit(entry.Title ?? string.Empty, 256);
            remaining -= content.Length;
            results.Add(new PageContent(uri.AbsoluteUri, content, title, retrievedAt,
                content.Length < (entry.Content?.Length ?? 0) || title.Length < (entry.Title?.Length ?? 0)));
        }
        return ResponseModel<IReadOnlyList<PageContent>>.Succeed(results);
    }

    private static string Limit(string value, int maximum)
    {
        if (value.Length <= maximum) return value;
        // Keep Unicode surrogate pairs intact at the boundary.
        if (maximum > 0 && char.IsHighSurrogate(value[maximum - 1])) maximum--;
        return value[..maximum];
    }
}
