using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyAIAgent.McpServer.Application.Interfaces;
using MyAIAgent.McpServer.Domain.Models;
namespace MyAIAgent.McpServer.Infrastructure.Services;

public sealed class GoogleSearchClient(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GoogleSearchClient> logger) : ISearchProvider
{
    private const string BaseUrl = "https://www.googleapis.com/customsearch/v1";

    public async IAsyncEnumerable<SearchEntry> SearchAsync(
        string query,
        int top,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["GoogleSearch:ApiKey"]
            ?? throw new InvalidOperationException("Configuration key 'GoogleSearch:ApiKey' is missing");
        var searchEngineId = configuration["GoogleSearch:SearchEngineId"]
            ?? throw new InvalidOperationException("Configuration key 'GoogleSearch:SearchEngineId' is missing");

        var url = $"{BaseUrl}?cx={Uri.EscapeDataString(searchEngineId)}&q={Uri.EscapeDataString(query)}&num={top}";

        GoogleSearchResponse? response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Goog-Api-Key", apiKey);
            using var result = await httpClient.SendAsync(request, cancellationToken);
            result.EnsureSuccessStatusCode();
            response = await result.Content.ReadFromJsonAsync<GoogleSearchResponse>(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Google Custom Search request failed");
            yield break;
        }

        if (response?.Items is null)
            yield break;

        foreach (var item in response.Items)
            yield return new SearchEntry(item.Link, item.Snippet);
    }

    private sealed record GoogleSearchResponse(
        [property: JsonPropertyName("items")] List<GoogleSearchItem>? Items);

    private sealed record GoogleSearchItem(
        [property: JsonPropertyName("link")] string? Link,
        [property: JsonPropertyName("snippet")] string? Snippet);
}
