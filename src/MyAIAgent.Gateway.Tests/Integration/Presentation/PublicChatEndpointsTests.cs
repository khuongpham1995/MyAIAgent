using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using MyAIAgent.Common.Domain.Models;
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

    [Fact]
    public async Task ChatStream_WhenVisitorExceedsBurst_ShouldReturnTooManyRequests()
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
            var response = await app.Client.SendAsync(PublicChatTestApp.ChatRequest(token));
            await response.Content.ReadAsStringAsync();
            statuses.Add(response.StatusCode);
        }

        // Assert
        statuses.Should().Equal(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ChatStream_WhenVisitorHoldsAllTheirStreamSlots_ShouldRejectTheNextStreamOnly()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync(
            new PublicChatRateLimits(IpPermitLimit: 100, MaxConcurrentStreamsPerVisitor: 1));
        var release = new TaskCompletionSource();
        app.StreamClient
            .Setup(c => c.StreamChatAsync(
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(() => HoldOpenAsync(release.Task));

        // Act
        var first = await app.Client.SendAsync(
            PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken("visitor-a")),
            HttpCompletionOption.ResponseHeadersRead);
        var secondSameVisitor = await app.Client.SendAsync(
            PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken("visitor-a")));
        var otherVisitor = await app.Client.SendAsync(
            PublicChatTestApp.ChatRequest(PublicChatTestApp.CreateToken("visitor-b")),
            HttpCompletionOption.ResponseHeadersRead);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        secondSameVisitor.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        otherVisitor.StatusCode.Should().Be(HttpStatusCode.OK);

        release.SetResult();
        await first.Content.ReadAsStringAsync();
        await otherVisitor.Content.ReadAsStringAsync();
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
        var request = new HttpRequestMessage(HttpMethod.Get, "/public/conversations?userId=visitor:other");
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
        var request = new HttpRequestMessage(HttpMethod.Get, "/public/conversations/s-404");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PublicChatTestApp.CreateToken("visitor-a"));

        // Act
        var response = await app.Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Conversations_WhenRenamedWithValidTitle_ShouldReturnNoContent()
    {
        // Arrange
        await using var app = await PublicChatTestApp.StartAsync();
        app.OrchestratorClient
            .Setup(c => c.RenameConversationAsync("s-1", "visitor:visitor-a", "New title", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<bool>.Succeed(true));
        var request = new HttpRequestMessage(HttpMethod.Put, "/public/conversations/s-1/title")
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
        var request = new HttpRequestMessage(HttpMethod.Options, "/public/chat/stream");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }
}
