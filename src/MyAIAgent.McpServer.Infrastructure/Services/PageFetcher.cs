/**
 * MyAIAgent | McpServer · Infrastructure
 *
 * Retrieves bounded public page responses, follows validated redirects, and extracts readable text.
 */

using Microsoft.Extensions.Logging;
using MyAIAgent.McpServer.Application.Common.Interfaces;
using MyAIAgent.McpServer.Application.Search;

namespace MyAIAgent.McpServer.Infrastructure.Services;

public sealed class PageFetcher(ILogger<PageFetcher> logger, HttpClient httpClient) : IPageFetcher
{
    private const int MaxRedirects = 5;
    private const int MaxContentBytes = 1024 * 1024;

    public async Task<FetchResult> GetContentAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return new FetchResult(false, Error: "Invalid page URL.");

        for (var redirectCount = 0; redirectCount <= MaxRedirects; redirectCount++)
        {
            if (!PublicPageAddressPolicy.IsAllowedUri(uri))
                return new FetchResult(false, Error: "Page destination is not allowed.");

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await httpClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if ((int)response.StatusCode is >= 300 and <= 399 && response.Headers.Location is { } location)
                {
                    var nextUri = new Uri(uri, location);
                    if (uri.Scheme == Uri.UriSchemeHttps && nextUri.Scheme != Uri.UriSchemeHttps)
                        return new FetchResult(false, Error: "Page redirect is not allowed.");

                    uri = nextUri;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Page fetch returned HTTP {StatusCode}", (int)response.StatusCode);
                    return new FetchResult(false, Error: $"HTTP error: {(int)response.StatusCode}");
                }

                if (response.Content.Headers.ContentLength > MaxContentBytes)
                    return new FetchResult(false, Error: "Page content is too large.");

                await response.Content.LoadIntoBufferAsync(MaxContentBytes, cancellationToken);
                var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return new FetchResult(true, content, contentType);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException)
            {
                logger.LogWarning("Page fetch failed");
                return new FetchResult(false, Error: "Failed to retrieve page content.");
            }
        }

        return new FetchResult(false, Error: "Too many page redirects.");
    }
}
