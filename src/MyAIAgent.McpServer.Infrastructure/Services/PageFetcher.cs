using System.Net;
using Microsoft.Extensions.Logging;
using MyAIAgent.McpServer.Domain.Interfaces;
using MyAIAgent.McpServer.Domain.Models;

namespace MyAIAgent.McpServer.Infrastructure.Services;

public sealed class PageFetcher(ILogger<PageFetcher> logger, HttpClient httpClient) : IPageFetcher
{
    public async Task<FetchResult> GetContentAsync(string url, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(url);
        var scheme = uri.Scheme.ToUpperInvariant();

        if (scheme is not "HTTP" and not "HTTPS")
            return new FetchResult(false, Error: $"Unknown URL protocol: {uri.Scheme}");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(uri, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to fetch {Url}", url);
            return new FetchResult(false, Error: "Failed to retrieve page content.");
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("HTTP {StatusCode} fetching {Url}", (int)response.StatusCode, url);
            return new FetchResult(false, Error: $"HTTP error: {(int)response.StatusCode}");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        return new FetchResult(true, content, contentType);
    }
}
