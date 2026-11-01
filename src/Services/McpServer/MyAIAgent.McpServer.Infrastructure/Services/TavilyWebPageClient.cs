/**
 * MyAIAgent | McpServer · Infrastructure
 *
 * Submits one public URL to Tavily basic extraction with a bounded response and one end-to-end deadline.
 */

using MyAIAgent.Common.Application.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MyAIAgent.McpServer.Application.Common.Interfaces;
using MyAIAgent.McpServer.Application.Search;

namespace MyAIAgent.McpServer.Infrastructure.Services;

public sealed class TavilyWebPageClient(HttpClient httpClient, IConfiguration configuration, IPublicWebPageHostPolicy hostPolicy) : IWebPageReader
{
    public static readonly Uri Endpoint = new("https://api.tavily.com/extract");
    private const int MaximumResponseBytes = 1024 * 1024;

    public async Task<ResponseModel<string>> ReadAsync(Uri url, CancellationToken cancellationToken)
    {
        var key = configuration["Tavily:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return Failure("Webpage reading is not configured.");
        if (key.Any(char.IsWhiteSpace) || key.Any(char.IsControl)) return Failure("Webpage reading credentials are invalid.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            if (!await hostPolicy.IsAllowedAsync(url.IdnHost, deadline.Token))
                return Failure("Webpage URL must resolve only to public addresses.");

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = JsonContent.Create(new
            {
                urls = new[] { url.AbsoluteUri },
                extract_depth = "basic",
                format = "markdown",
                include_images = false,
                include_favicon = false,
                timeout = 10
            });
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
                return Failure((int)response.StatusCode switch
                {
                    401 or 403 => "Webpage reading credentials were rejected.",
                    429 or 432 or 433 => "Webpage reading quota or rate limit was reached.",
                    _ => "Webpage reading is temporarily unavailable."
                });

            if (response.Content.Headers.ContentLength > MaximumResponseBytes)
                return Failure("Webpage response exceeded the size limit.");
            await response.Content.LoadIntoBufferAsync(MaximumResponseBytes, deadline.Token);
            using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(deadline.Token));
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("failed_results", out var failures) || failures.ValueKind != JsonValueKind.Array)
                return Failure("Webpage reading returned an invalid response.");
            if (results.GetArrayLength() == 0 || failures.GetArrayLength() > 0)
                return Failure("The webpage could not be read.");
            if (results.GetArrayLength() != 1 || results[0].ValueKind != JsonValueKind.Object
                || !results[0].TryGetProperty("url", out var source) || source.ValueKind != JsonValueKind.String
                || !WebPageUrl.TryNormalize(source.GetString(), out var sourceUrl) || sourceUrl != url
                || !results[0].TryGetProperty("raw_content", out var content) || content.ValueKind != JsonValueKind.String)
                return Failure("Webpage reading returned an invalid response.");
            var text = content.GetString();
            return string.IsNullOrWhiteSpace(text) ? Failure("The webpage could not be read.") : ResponseModel<string>.Succeed(text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("Webpage reading timed out.");
        }
        catch (JsonException)
        {
            return Failure("Webpage reading returned an invalid response.");
        }
        catch (HttpRequestException)
        {
            return Failure("Webpage reading is temporarily unavailable.");
        }
        catch (IOException)
        {
            return Failure("Webpage reading is temporarily unavailable.");
        }
    }

    private static ResponseModel<string> Failure(string message) => ResponseModel<string>.Fail(message: message);
}
