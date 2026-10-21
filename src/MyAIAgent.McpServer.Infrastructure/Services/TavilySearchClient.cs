/**
 * MyAIAgent | McpServer · Infrastructure
 *
 * Calls the fixed Tavily basic-search endpoint with bounded responses and sanitized failures.
 */

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.McpServer.Application.Common.Interfaces;
using MyAIAgent.McpServer.Application.Search;

namespace MyAIAgent.McpServer.Infrastructure.Services;

public sealed class TavilySearchClient(HttpClient httpClient, IConfiguration configuration) : ISearchProvider
{
    public static readonly Uri Endpoint = new("https://api.tavily.com/search");
    private const int MaximumResponseBytes = 1024 * 1024;

    public async Task<ResponseModel<IReadOnlyList<SearchEntry>>> SearchAsync(string query, int top, CancellationToken cancellationToken = default)
    {
        var key = configuration["Tavily:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return Failure("Web search is not configured.");
        if (key.Any(char.IsWhiteSpace) || key.Any(char.IsControl)) return Failure("Web search credentials are invalid.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = JsonContent.Create(new
            {
                query,
                max_results = top,
                search_depth = "basic",
                topic = "general",
                auto_parameters = false,
                include_answer = false,
                include_raw_content = false,
                include_images = false
            });
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
                return Failure((int)response.StatusCode switch
                {
                    401 or 403 => "Web search credentials were rejected.",
                    429 or 432 or 433 => "Web search quota or rate limit was reached.",
                    _ => "Web search is temporarily unavailable."
                });

            if (response.Content.Headers.ContentLength > MaximumResponseBytes)
                return Failure("Web search response exceeded the size limit.");
            await response.Content.LoadIntoBufferAsync(MaximumResponseBytes, deadline.Token);
            using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(deadline.Token));
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return Failure("Web search returned an invalid response.");

            var entries = new List<SearchEntry>();
            foreach (var item in results.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !TryReadText(item, "url", out var url)
                    || !TryReadText(item, "title", out var title)
                    || !TryReadText(item, "content", out var content))
                    return Failure("Web search returned an invalid response.");
                entries.Add(new SearchEntry(url, title, content));
            }
            return ResponseModel<IReadOnlyList<SearchEntry>>.Succeed(entries);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("Web search timed out.");
        }
        catch (JsonException)
        {
            return Failure("Web search returned an invalid response.");
        }
        catch (HttpRequestException)
        {
            return Failure("Web search is temporarily unavailable.");
        }
        catch (IOException)
        {
            return Failure("Web search is temporarily unavailable.");
        }
    }

    private static bool TryReadText(JsonElement item, string name, out string? value)
    {
        value = null;
        if (!item.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString();
        return true;
    }

    private static ResponseModel<IReadOnlyList<SearchEntry>> Failure(string message) =>
        ResponseModel<IReadOnlyList<SearchEntry>>.Fail(message: message);
}
