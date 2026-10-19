using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MyAIAgent.Gateway.Application.Chat;
using MyAIAgent.Gateway.Infrastructure.Services;

namespace MyAIAgent.Gateway.Tests.Unit.Infrastructure.Services;

public sealed class OrchestratorStreamClientTests
{
    private const string SseBody =
        "event: session\ndata: {\"type\":\"session\",\"sessionId\":\"s-1\"}\n\n" +
        "event: delta\ndata: {\"type\":\"delta\",\"text\":\"Hi\"}\n\n" +
        "event: done\ndata: {\"type\":\"done\",\"sessionId\":\"s-1\"}\n\n";

    private static OrchestratorStreamClient BuildClient(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://orchestrator.test") },
            NullLogger<OrchestratorStreamClient>.Instance);

    private static async Task<List<ChatStreamEvent>> CollectAsync(IAsyncEnumerable<ChatStreamEvent> events)
    {
        var collected = new List<ChatStreamEvent>();
        await foreach (var chatEvent in events)
            collected.Add(chatEvent);
        return collected;
    }

    private static HttpResponseMessage SseResponse(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };

    [Fact]
    public async Task StreamChatAsync_WhenOrchestratorStreams_ShouldParseEventsInOrder()
    {
        var handler = new StubHttpMessageHandler(_ => SseResponse(SseBody));

        var events = await CollectAsync(
            BuildClient(handler).StreamChatAsync(null, "Hello", "visitor:a", "agent-1", CancellationToken.None));

        events.Select(e => e.Type).Should().Equal("session", "delta", "done");
        events[0].SessionId.Should().Be("s-1");
        events[1].Text.Should().Be("Hi");
    }

    [Fact]
    public async Task StreamChatAsync_WhenCalled_ShouldPostIdentityAndAgentToOrchestrator()
    {
        var handler = new StubHttpMessageHandler(_ => SseResponse(SseBody));

        await CollectAsync(
            BuildClient(handler).StreamChatAsync("s-1", "Hello", "visitor:a", "agent-1", CancellationToken.None));

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.Should().Be("/chat/stream");
    }

    [Fact]
    public async Task StreamChatAsync_WhenOrchestratorReturnsErrorStatus_ShouldYieldSafeErrorEvent()
    {
        var handler = new StubHttpMessageHandler(_ => SseResponse("boom internal details", HttpStatusCode.InternalServerError));

        var events = await CollectAsync(
            BuildClient(handler).StreamChatAsync(null, "Hello", "visitor:a", null, CancellationToken.None));

        var error = events.Should().ContainSingle().Subject;
        error.Type.Should().Be("error");
        error.Message.Should().Be("Chat request failed.");
    }

    [Fact]
    public async Task StreamChatAsync_WhenOrchestratorIsUnreachable_ShouldYieldSafeErrorEvent()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection refused to 10.0.0.5"));

        var events = await CollectAsync(
            BuildClient(handler).StreamChatAsync(null, "Hello", "visitor:a", null, CancellationToken.None));

        var error = events.Should().ContainSingle().Subject;
        error.Message.Should().Be("Chat request failed.");
        error.Message.Should().NotContain("10.0.0.5");
    }

    [Fact]
    public async Task StreamChatAsync_WhenPayloadIsNotJson_ShouldYieldErrorEventAfterEarlierEvents()
    {
        const string body =
            "event: delta\ndata: {\"type\":\"delta\",\"text\":\"Hi\"}\n\n" +
            "event: delta\ndata: not-json\n\n";
        var handler = new StubHttpMessageHandler(_ => SseResponse(body));

        var events = await CollectAsync(
            BuildClient(handler).StreamChatAsync(null, "Hello", "visitor:a", null, CancellationToken.None));

        events.Select(e => e.Type).Should().Equal("delta", "error");
    }

    [Fact]
    public async Task StreamChatAsync_WhenHttpClientTimesOut_ShouldYieldErrorEventInsteadOfThrowing()
    {
        var handler = new ThrowingHttpMessageHandler(new TaskCanceledException("timeout"));

        var events = await CollectAsync(
            BuildClient(handler).StreamChatAsync(null, "Hello", "visitor:a", null, CancellationToken.None));

        events.Should().ContainSingle().Which.Type.Should().Be("error");
    }

    [Fact]
    public async Task StreamChatAsync_WhenCallerCancels_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var handler = new ThrowingHttpMessageHandler(new OperationCanceledException(cancellation.Token));

        var act = async () => await CollectAsync(
            BuildClient(handler).StreamChatAsync(null, "Hello", "visitor:a", null, cancellation.Token));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StreamChatAsync_WhenStreamEndsWithoutTerminalEvent_ShouldAppendSafeErrorEvent()
    {
        const string body =
            "event: session\ndata: {\"type\":\"session\",\"sessionId\":\"s-1\"}\n\n" +
            "event: delta\ndata: {\"type\":\"delta\",\"text\":\"Hi\"}\n\n";
        var handler = new StubHttpMessageHandler(_ => SseResponse(body));

        var events = await CollectAsync(
            BuildClient(handler).StreamChatAsync(null, "Hello", "visitor:a", null, CancellationToken.None));

        events.Select(e => e.Type).Should().Equal("session", "delta", "error");
        events[^1].Message.Should().Be("Chat request failed.");
    }

    [Fact]
    public async Task StreamChatAsync_WhenOrchestratorSendsNothing_ShouldYieldSafeErrorEvent()
    {
        var handler = new StubHttpMessageHandler(_ => SseResponse(string.Empty));

        var events = await CollectAsync(
            BuildClient(handler).StreamChatAsync(null, "Hello", "visitor:a", null, CancellationToken.None));

        events.Should().ContainSingle().Which.Type.Should().Be("error");
    }

    [Fact]
    public async Task StreamChatAsync_WhenOrchestratorEndsWithItsOwnErrorEvent_ShouldNotAddAnotherError()
    {
        const string body =
            "event: session\ndata: {\"type\":\"session\",\"sessionId\":\"s-1\"}\n\n" +
            "event: error\ndata: {\"type\":\"error\",\"message\":\"AI service temporarily unavailable.\"}\n\n";
        var handler = new StubHttpMessageHandler(_ => SseResponse(body));

        var events = await CollectAsync(
            BuildClient(handler).StreamChatAsync(null, "Hello", "visitor:a", null, CancellationToken.None));

        events.Select(e => e.Type).Should().Equal("session", "error");
        events[^1].Message.Should().Be("AI service temporarily unavailable.");
    }
}
