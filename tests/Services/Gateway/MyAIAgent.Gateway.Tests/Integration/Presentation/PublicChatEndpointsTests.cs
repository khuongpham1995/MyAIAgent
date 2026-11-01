/**
 * MyAIAgent | Gateway · Tests
 *
 * Exercises visitor JWT authentication, origin controls, rate limits, streaming, and conversation ownership over HTTP.
 */

using MyAIAgent.Common.Application.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using MyAIAgent.Common.Tests;
using MyAIAgent.Gateway.Application.Chat;
using MyAIAgent.Gateway.Application.Conversations;
using MyAIAgent.Gateway.Presentation.PublicApi;

namespace MyAIAgent.Gateway.Tests.Integration.Presentation;

public sealed class PublicChatEndpointsTests
{
    private static void SetupStream(PublicChatTestApp app) =>
        app.StreamClient
            .Setup(c => c.StreamChatAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(() => AsyncEnumerableHelper.ToAsyncEnumerable(new[]
            {
                new ChatStreamEvent("session", SessionId: "s-1"),
                new ChatStreamEvent("delta", Text: "Hello"),
                new ChatStreamEvent("done", SessionId: "s-1"),
            }));

    [Fact]
    public async Task Routes_WhenOldOrSimilarPublicPathsRequested_ShouldNotMatchOrConsumePublicBudget()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync(new PublicChatRateLimits(IpPermitLimit: 1));
        SetupStream(app);

        // Act / Assert
        foreach (var (method, path) in new[] { ("POST", "/public/chat/stream"), ("GET", "/public/conversations"), ("GET", "/api/publicity/conversations"), ("POST", "/sse/publicity/chat") })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            using var response = await app.Client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            response.Headers.Location.Should().BeNull();
        }
        using var valid = await app.Client.SendAsync(PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken()));
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChatStream_WhenTokenIsValid_ShouldStreamEventsAsServerSentEvents()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync();
        SetupStream(app);

        // Act
        var response = await app.Client.SendAsync(PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken()));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        body.Should().Contain("event: session").And.Contain("event: delta").And.Contain("event: done");
        body.Should().Contain("\"text\":\"Hello\"");
        body.Should().NotContain("\"title\":null");
    }

    [Fact]
    public async Task ChatStream_WhenClientSendsAgentId_ShouldUseConfiguredAgentAndVisitorIdentity()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync();
        SetupStream(app);
        const string body = "{\"sessionId\":\"s-9\",\"userMessage\":\"Hi\",\"agentId\":\"attacker-agent\",\"userId\":\"tid:oid\"}";

        // Act
        var response = await app.Client.SendAsync(
            PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken("visitor-a"), body));
        await response.Content.ReadAsStringAsync();

        // Assert
        app.StreamClient.Verify(
            c => c.StreamChatAsync(
                "s-9", "Hi", "visitor:visitor-a", PublicChatTestApp.ConfiguredAgent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChatStream_WhenMessageIsEmpty_ShouldRejectWithBadRequestBeforeStreaming()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync();
        SetupStream(app);

        // Act
        var response = await app.Client.SendAsync(
            PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken(), "{\"userMessage\":\"\"}"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        app.StreamClient.Verify(
            c => c.StreamChatAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public static TheoryData<string, string> RejectedTokens => new()
    {
        { "no token", "" },
        { "wrong issuer", PublicChatTestApp.CreateToken(issuer: "https://evil.test") },
        { "wrong audience", PublicChatTestApp.CreateToken(audience: "other-api") },
        { "expired", PublicChatTestApp.CreateToken(expires: DateTime.UtcNow.AddMinutes(-30)) },
        { "bad signature", PublicChatTestApp.CreateToken(signingKey: Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64))) },
        { "missing sub", PublicChatTestApp.CreateToken(subject: null) },
        { "overlong sub", PublicChatTestApp.CreateToken(subject: new string('a', 129)) },
        { "unexpected algorithm", PublicChatTestApp.CreateToken(algorithm: SecurityAlgorithms.HmacSha384) },
    };

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task ChatStream_WhenTokenIsNotAcceptable_ShouldReturnUnauthorized(string scenario, string token)
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync();
        SetupStream(app);

        // Act
        var response = await app.Client.SendAsync(
            PublicChatTestApp.ChatRequest(token.Length == 0 ? null : token));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, scenario);
        app.StreamClient.Verify(
            c => c.StreamChatAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Preflight_WhenOriginIsAllowed_ShouldReturnCorsHeaders()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync();
        var request = PreflightRequest(PublicChatTestApp.AllowedOrigin);

        // Act
        var response = await app.Client.SendAsync(request);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal(PublicChatTestApp.AllowedOrigin);
        response.Headers.GetValues("Access-Control-Allow-Headers").Single().Should().Contain("Authorization");
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
    }

    [Fact]
    public async Task Preflight_WhenOriginIsNotAllowed_ShouldNotReturnAllowOriginHeader()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync();

        // Act
        var response = await app.Client.SendAsync(PreflightRequest("https://evil.test"));

        // Assert
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task ChatStream_WhenIpExceedsLimit_ShouldReturnTooManyRequestsWithRetryAfter()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync(new PublicChatRateLimits(IpPermitLimit: 2));

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;

        // Act
        for (var attempt = 0; attempt < 3; attempt++)
        {
            last = await app.Client.SendAsync(PublicChatTestApp.ChatRequest(null));
            statuses.Add(last.StatusCode);
        }

        // Assert
        statuses.Should().Equal(HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
        last!.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Theory]
    [InlineData("/sse/public/chat")]
    [InlineData("/sse/public/chat/")]
    [InlineData("/SSE/PUBLIC/CHAT/")]
    public async Task ChatStream_WhenVisitorExceedsBurst_ShouldReturnTooManyRequests(string path)
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync(
            new PublicChatRateLimits(IpPermitLimit: 100, VisitorBurst: 2, VisitorReplenishSeconds: 3600));
        SetupStream(app);
        var token = PublicChatTestApp.CreateToken("visitor-a");

        var statuses = new List<HttpStatusCode>();

        // Act
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await app.Client.SendAsync(
                PublicChatTestApp.ChatRequest(token, path: attempt == 0 ? "/sse/public/chat" : path));
            await response.Content.ReadAsStringAsync();
            statuses.Add(response.StatusCode);
        }

        // Assert
        statuses.Should().Equal(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests);
        app.StreamClient.Verify(
            c => c.StreamChatAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Theory]
    [InlineData("/sse/public/chat")]
    [InlineData("/sse/public/chat/")]
    [InlineData("/SSE/PUBLIC/CHAT/")]
    public async Task ChatStream_WhenVisitorHoldsAllTheirStreamSlots_ShouldRejectTheNextStreamOnly(string path)
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync(
            new PublicChatRateLimits(IpPermitLimit: 100, MaxConcurrentStreamsPerVisitor: 1));
        var release = new TaskCompletionSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        app.StreamClient
            .Setup(c => c.StreamChatAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(() => HoldOpenAsync(release.Task));

        try
        {
            // Act
            using var first = await app.Client.SendAsync(
                PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken("visitor-a")),
                HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            using var secondSameVisitor = await app.Client.SendAsync(
                PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken("visitor-a"), path: path),
                HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            using var otherVisitor = await app.Client.SendAsync(
                PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken("visitor-b")),
                HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            release.TrySetResult();
            await first.Content.ReadAsStringAsync(deadline.Token);
            await otherVisitor.Content.ReadAsStringAsync(deadline.Token);

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.OK);
            secondSameVisitor.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            otherVisitor.StatusCode.Should().Be(HttpStatusCode.OK);
            app.StreamClient.Verify(
                c => c.StreamChatAsync(
                    It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Theory]
    [InlineData("/sse/public/chat")]
    [InlineData("/sse/public/chat/")]
    [InlineData("/SSE/PUBLIC/CHAT/")]
    public async Task ChatStream_WhenGlobalStreamSlotsAreFull_ShouldRejectAnotherVisitor(string path)
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync(
            new PublicChatRateLimits(IpPermitLimit: 100, MaxConcurrentStreams: 1));
        var release = new TaskCompletionSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        app.StreamClient
            .Setup(c => c.StreamChatAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(() => HoldOpenAsync(release.Task));

        try
        {
            // Act
            using var first = await app.Client.SendAsync(
                PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken("visitor-a")),
                HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            using var second = await app.Client.SendAsync(
                PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken("visitor-b"), path: path),
                HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            release.TrySetResult();
            await first.Content.ReadAsStringAsync(deadline.Token);

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.OK);
            second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            app.StreamClient.Verify(
                c => c.StreamChatAsync(
                    It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Theory]
    [InlineData("/sse/public/chat/")]
    [InlineData("/SSE/PUBLIC/CHAT/")]
    public async Task ChatStream_WhenEquivalentPathHasNoToken_ShouldRejectBeforeDownstreamCall(string path)
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync();
        SetupStream(app);

        // Act
        using var response = await app.Client.SendAsync(PublicChatTestApp.ChatRequest(null, path: path));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        app.StreamClient.Verify(
            c => c.StreamChatAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async IAsyncEnumerable<ChatStreamEvent> HoldOpenAsync(Task release)
    {
        yield return new ChatStreamEvent("session", SessionId: "s-1");
        await release;
        yield return new ChatStreamEvent("done", SessionId: "s-1");
    }

    [Fact]
    public async Task ChatStream_WhenAnotherVisitorHasExhaustedBurst_ShouldStillServeThisVisitor()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync(
            new PublicChatRateLimits(IpPermitLimit: 100, VisitorBurst: 1, VisitorReplenishSeconds: 3600));
        SetupStream(app);

        // Act
        var first = await app.Client.SendAsync(PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken("visitor-a")));
        await first.Content.ReadAsStringAsync();
        var second = await app.Client.SendAsync(PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken("visitor-b")));
        await second.Content.ReadAsStringAsync();

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Conversations_WhenVisitorLists_ShouldQueryOnlyTheirOwnVisitorId()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync();
        app.OrchestratorClient
            .Setup(c => c.ListConversationsAsync("visitor:visitor-a", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<IReadOnlyList<ConversationSummary>>.Succeed(
                [new ConversationSummary("s-1", "Title", DateTimeOffset.UtcNow)]));
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/conversations?userId=visitor:other");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PublicChatTestApp.CreateToken("visitor-a"));

        // Act
        var response = await app.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("s-1");
        app.OrchestratorClient.Verify(
            c => c.ListConversationsAsync("visitor:visitor-a", 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Conversations_WhenOrchestratorReportsNotFound_ShouldReturnNotFound()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync();
        app.OrchestratorClient
            .Setup(c => c.GetConversationAsync("s-404", "visitor:visitor-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<ConversationDetail>.Fail(message: "Conversation not found."));
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/conversations/s-404");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PublicChatTestApp.CreateToken("visitor-a"));

        // Act
        var response = await app.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Conversations_WhenDeleteFails_ShouldReturnTypedErrorWithMatchingStatus()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync();
        app.OrchestratorClient
            .Setup(client => client.DeleteConversationAsync("s-404", "visitor:visitor-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<bool>.Fail(message: "Conversation not found."));
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/public/conversations/s-404");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PublicChatTestApp.CreateToken("visitor-a"));

        // Act
        using var response = await app.Client.SendAsync(request);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("message").GetString().Should().Be("Conversation not found.");
        body.RootElement.GetProperty("data").GetProperty("statusCode").GetInt32().Should().Be(404);
        body.RootElement.GetProperty("errors").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task Conversations_WhenRenamedWithValidTitle_ShouldReturnNoContent()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync();
        app.OrchestratorClient
            .Setup(c => c.RenameConversationAsync("s-1", "visitor:visitor-a", "New title", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<bool>.Succeed(true));
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/public/conversations/s-1/title")
        {
            Content = new StringContent("{\"title\":\"New title\"}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PublicChatTestApp.CreateToken("visitor-a"));

        // Act
        var response = await app.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static HttpRequestMessage PreflightRequest(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/sse/public/chat");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }
}
