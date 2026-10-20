/**
 * MyAIAgent | McpServer · Tests
 *
 * Checks provider request construction and search-result parsing without calling Google.
 */

using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MyAIAgent.McpServer.Infrastructure.Services;

namespace MyAIAgent.McpServer.Tests.Unit.Infrastructure.Services;

public sealed class GoogleSearchClientTests
{
    [Fact]
    public async Task SearchAsync_WhenRequestIsSent_ShouldKeepApiKeyOutOfUri()
    {
        // Arrange
        const string apiKey = "test-secret-key";
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSearch:ApiKey"] = apiKey,
                ["GoogleSearch:SearchEngineId"] = "engine-id"
            })
            .Build();
        var client = new GoogleSearchClient(httpClient, configuration, NullLogger<GoogleSearchClient>.Instance);

        var entries = new List<string>();

        // Act
        await foreach (var entry in client.SearchAsync("test query", 1))
            entries.Add(entry.Link!);

        // Assert
        entries.Should().ContainSingle().Which.Should().Be("https://example.com/result");
        handler.RequestUri.Should().NotBeNull();
        handler.RequestUri!.ToString().Should().NotContain(apiKey);
        handler.ApiKey.Should().Be(apiKey);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("X-Goog-Api-Key").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"items\":[{\"link\":\"https://example.com/result\",\"snippet\":\"Result\"}]}",
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
