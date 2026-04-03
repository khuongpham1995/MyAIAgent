using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyAIAgent.McpServer.Domain.Interfaces;
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

        var url = $"{BaseUrl}?key={apiKey}&cx={searchEngineId}&q={Uri.EscapeDataString(query)}&num={top}";

        GoogleSearchResponse? response;
        try
        {
            response = await httpClient.GetFromJsonAsync<GoogleSearchResponse>(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Google Custom Search request failed for query {Query}", query);
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
