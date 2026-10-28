/**
 * MyAIAgent | McpServer · Tests
 *
 * Checks fixed provider requests, safe failures, response bounds, and cancellation without live search calls.
 */

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MyAIAgent.McpServer.Infrastructure.Services;

namespace MyAIAgent.McpServer.Tests.Unit.Infrastructure.Services;

public sealed class TavilySearchClientTests
{
    [Fact]
    public async Task SearchAsync_WhenConfigured_ShouldUseOnlyBasicSearchAndMapExcerpts()
    {
        // Arrange
        string? body = null;
        var handler = new StubHandler(async (request, token) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri.Should().Be(TavilySearchClient.Endpoint);
            request.RequestUri!.Query.Should().BeEmpty();
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");
            request.Headers.Authorization.Parameter.Should().Be("test-key");
            body = await request.Content!.ReadAsStringAsync(token);
            return Response("{\"results\":[{\"url\":\"https://example.com/\",\"title\":\"Title\",\"content\":\"Excerpt\"}]}");
        });
        using var http = new HttpClient(handler);
        var client = Client(http);

        // Act
        var result = await client.SearchAsync("focused query", 4);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Single().Content.Should().Be("Excerpt");
        result.Data!.Single().Title.Should().Be("Title");
        using var json = JsonDocument.Parse(body!);
        var data = json.RootElement;
        data.GetProperty("query").GetString().Should().Be("focused query");
        data.GetProperty("max_results").GetInt32().Should().Be(4);
        data.GetProperty("search_depth").GetString().Should().Be("basic");
        data.GetProperty("topic").GetString().Should().Be("general");
        foreach (var name in new[] { "auto_parameters", "include_answer", "include_raw_content", "include_images" })
            data.GetProperty(name).GetBoolean().Should().BeFalse();
        data.EnumerateObject().Should().HaveCount(8);
        body.Should().NotContain("test-key");
        handler.Requests.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\r\ninvalid")]
    public async Task SearchAsync_WhenKeyIsAbsentOrInvalid_ShouldFailWithoutNetwork(string? key)
    {
        // Arrange
        var handler = new StubHandler((_, _) => Task.FromResult(Response("{}")));
        using var http = new HttpClient(handler);
        var client = Client(http, key);

        // Act
        var result = await client.SearchAsync("query", 3);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().StartWith("Web search");
        handler.Requests.Should().Be(0);
    }

    [Theory]
    [InlineData(401, "credentials")]
    [InlineData(403, "credentials")]
    [InlineData(429, "quota")]
    [InlineData(432, "quota")]
    [InlineData(433, "quota")]
    [InlineData(500, "unavailable")]
    [InlineData(302, "unavailable")]
    public async Task SearchAsync_WhenProviderRejects_ShouldReturnSafeFailureWithoutRetry(int status, string expected)
    {
        // Arrange
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        { Content = new StringContent("private details test-key query") }));
        using var http = new HttpClient(handler);
        var client = Client(http);

        // Act
        var result = await client.SearchAsync("query", 3);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(expected).And.NotContain("test-key").And.NotContain("private");
        handler.Requests.Should().Be(1);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"results\":null}")]
    [InlineData("{\"results\":[null]}")]
    [InlineData("{\"results\":[{\"url\":5,\"title\":\"title\",\"content\":\"text\"}]}")]
    [InlineData("{\"results\":[{\"url\":\"https://example.com/\"}]}")]
    public async Task SearchAsync_WhenPayloadIsMalformed_ShouldNotReportEmptySuccess(string body)
    {
        // Arrange
        var handler = new StubHandler((_, _) => Task.FromResult(Response(body)));
        using var http = new HttpClient(handler);
        var client = Client(http);

        // Act
        var result = await client.SearchAsync("query", 3);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Web search returned an invalid response.");
    }

    [Fact]
    public async Task SearchAsync_WhenResultsAreEmpty_ShouldReturnSuccessfulEmptyList()
    {
        // Arrange
        var handler = new StubHandler((_, _) => Task.FromResult(Response("{\"results\":[]}")));
        using var http = new HttpClient(handler);
        var client = Client(http);

        // Act
        var result = await client.SearchAsync("query", 3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SearchAsync_WhenBodyExceedsLimit_ShouldRejectKnownAndUnknownLengths(bool knownLength)
    {
        // Arrange
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = knownLength ? new ByteArrayContent(new byte[1024 * 1024 + 1]) : new UnknownLengthContent() }));
        using var http = new HttpClient(handler);
        var client = Client(http);

        // Act
        var result = await client.SearchAsync("query", 3);

        // Assert
        result.IsSuccess.Should().BeFalse();
        handler.Requests.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_WhenCallerCancelsDuringBody_ShouldPropagateCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var content = new BlockingContent();
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var http = new HttpClient(handler);
        var client = Client(http);
        var pending = client.SearchAsync("query", 3, cancellation.Token);
        await content.Started.Task;

        // Act
        cancellation.Cancel();
        var action = () => pending;

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SearchAsync_WhenDeadlineExpiresDuringBody_ShouldReturnTimeout()
    {
        // Arrange
        var content = new BlockingContent();
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var http = new HttpClient(handler);
        var client = Client(http);

        // Act
        var result = await client.SearchAsync("query", 3);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Web search timed out.");
        handler.Requests.Should().Be(1);
    }

    private static TavilySearchClient Client(HttpClient http, string? key = "test-key") => new(http,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Tavily:ApiKey"] = key }).Build());

    private static HttpResponseMessage Response(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return send(request, cancellationToken);
        }
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(new byte[1024 * 1024 + 1]).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    private sealed class BlockingContent : HttpContent
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
