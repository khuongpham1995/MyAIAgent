/**
 * MyAIAgent | McpServer · Tests
 *
 * Exercises fixed extraction requests, public-host checks, safe provider failures, response bounds, and cancellation.
 */

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using MyAIAgent.McpServer.Infrastructure.Services;

namespace MyAIAgent.McpServer.Tests.Unit.Infrastructure.Services;

public sealed class TavilyWebPageClientTests
{
    [Fact]
    public async Task ReadAsync_WhenConfigured_ShouldSendOneBasicMarkdownExtraction()
    {
        // Arrange
        var handler = new StubHandler(async (request, token) =>
        {
            request.RequestUri.Should().Be(TavilyWebPageClient.Endpoint);
            request.Method.Should().Be(HttpMethod.Post);
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");
            request.Headers.Authorization.Parameter.Should().Be("test-key");
            var body = await request.Content!.ReadAsStringAsync(token);
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            root.GetProperty("urls").EnumerateArray().Select(url => url.GetString()).Should().Equal("https://example.com/article");
            root.GetProperty("extract_depth").GetString().Should().Be("basic");
            root.GetProperty("format").GetString().Should().Be("markdown");
            root.GetProperty("include_images").GetBoolean().Should().BeFalse();
            root.GetProperty("include_favicon").GetBoolean().Should().BeFalse();
            root.GetProperty("timeout").GetInt32().Should().Be(10);
            root.EnumerateObject().Should().HaveCount(6);
            body.Should().NotContain("test-key");
            return Response(Success());
        });
        using var http = new HttpClient(handler);

        // Act
        var result = await Client(http).ReadAsync(new Uri("https://example.com/article"), CancellationToken.None);

        // Assert
        result.Data.Should().Be("# Article");
        handler.Requests.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad\nkey")]
    public async Task ReadAsync_WhenCredentialsAreMissingOrInvalid_ShouldAvoidDnsAndHttp(string? key)
    {
        // Arrange
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(Success())));
        using var http = new HttpClient(handler);
        var policy = new Mock<IPublicWebPageHostPolicy>(MockBehavior.Strict);

        // Act
        var result = await Client(http, policy.Object, key).ReadAsync(new Uri("https://example.com/article"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        policy.VerifyNoOtherCalls();
        handler.Requests.Should().Be(0);
    }

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("168.63.129.16")]
    [InlineData("::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:192.168.1.1")]
    public async Task ReadAsync_WhenDnsIncludesNonpublicAnswer_ShouldRejectBeforeTavily(string address)
    {
        // Arrange
        var policy = new PublicWebPageHostPolicy((_, _) => Task.FromResult(new[] { IPAddress.Parse("1.1.1.1"), IPAddress.Parse(address) }));
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(Success())));
        using var http = new HttpClient(handler);

        // Act
        var result = await Client(http, policy).ReadAsync(new Uri("https://example.com/article"), CancellationToken.None);

        // Assert
        result.Message.Should().Be("Webpage URL must resolve only to public addresses.");
        handler.Requests.Should().Be(0);
    }

    [Fact]
    public async Task IsAllowedAsync_WhenDnsIsEmptyOrFails_ShouldDeny()
    {
        // Arrange
        var empty = new PublicWebPageHostPolicy((_, _) => Task.FromResult(Array.Empty<IPAddress>()));
        var failed = new PublicWebPageHostPolicy((_, _) => throw new SocketException());

        // Act
        var answers = await Task.WhenAll(empty.IsAllowedAsync("example.com", CancellationToken.None), failed.IsAllowedAsync("example.com", CancellationToken.None));

        // Assert
        answers.Should().OnlyContain(answer => !answer);
    }

    [Theory]
    [InlineData(401, "credentials")]
    [InlineData(403, "credentials")]
    [InlineData(429, "quota")]
    [InlineData(432, "quota")]
    [InlineData(433, "quota")]
    [InlineData(302, "unavailable")]
    [InlineData(500, "unavailable")]
    public async Task ReadAsync_WhenProviderRejects_ShouldReturnSafeFailureWithoutRetry(int status, string message)
    {
        // Arrange
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        { Content = new StringContent("private provider details test-key") }));
        using var http = new HttpClient(handler);

        // Act
        var result = await Client(http).ReadAsync(new Uri("https://example.com/article"), CancellationToken.None);

        // Assert
        result.Message.Should().Contain(message).And.NotContain("private").And.NotContain("test-key");
        handler.Requests.Should().Be(1);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"results\":[],\"failed_results\":[{\"error\":\"private error\"}]}")]
    [InlineData("{\"results\":[],\"failed_results\":[]}")]
    [InlineData("{\"results\":[null],\"failed_results\":[]}")]
    [InlineData("{\"results\":[{\"url\":\"https://wrong.example/\",\"raw_content\":\"text\"}],\"failed_results\":[]}")]
    [InlineData("{\"results\":[{\"url\":\"https://example.com/article\",\"raw_content\":\" \"}],\"failed_results\":[]}")]
    public async Task ReadAsync_WhenHttp200ContainsNoUsableExtract_ShouldFail(string body)
    {
        // Arrange
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(body)));
        using var http = new HttpClient(handler);

        // Act
        var result = await Client(http).ReadAsync(new Uri("https://example.com/article"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Message.Should().NotContain("private error");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadAsync_WhenBodyExceedsLimit_ShouldRejectKnownAndUnknownLengths(bool knownLength)
    {
        // Arrange
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = knownLength ? new ByteArrayContent(new byte[1024 * 1024 + 1]) : new UnknownLengthContent() }));
        using var http = new HttpClient(handler);

        // Act
        var result = await Client(http).ReadAsync(new Uri("https://example.com/article"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        handler.Requests.Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadAsync_WhenCallerCancels_ShouldPropagateAcrossDnsAndBody(bool duringDns)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = new PublicWebPageHostPolicy(async (_, token) =>
        {
            if (duringDns) { started.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            return [IPAddress.Parse("1.1.1.1")];
        });
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new BlockingContent(started) }));
        using var http = new HttpClient(handler);
        var pending = Client(http, policy).ReadAsync(new Uri("https://example.com/article"), cancellation.Token);
        await started.Task;

        // Act
        cancellation.Cancel();
        var action = () => pending;

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        handler.Requests.Should().Be(duringDns ? 0 : 1);
    }

    [Fact]
    public async Task ReadAsync_WhenDeadlineExpiresDuringBody_ShouldReturnSafeTimeout()
    {
        // Arrange
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new BlockingContent(new TaskCompletionSource()) }));
        using var http = new HttpClient(handler);

        // Act
        var result = await Client(http).ReadAsync(new Uri("https://example.com/article"), CancellationToken.None);

        // Assert
        result.Message.Should().Be("Webpage reading timed out.");
        handler.Requests.Should().Be(1);
    }

    private static TavilyWebPageClient Client(HttpClient http, IPublicWebPageHostPolicy? policy = null, string? key = "test-key") =>
        new(http, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Tavily:ApiKey"] = key }).Build(),
            policy ?? new PublicWebPageHostPolicy((_, _) => Task.FromResult(new[] { IPAddress.Parse("1.1.1.1") })));

    private static string Success() => JsonSerializer.Serialize(new { results = new[] { new { url = "https://example.com/article", raw_content = "# Article" } }, failed_results = Array.Empty<object>() });
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

    private sealed class BlockingContent(TaskCompletionSource started) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
