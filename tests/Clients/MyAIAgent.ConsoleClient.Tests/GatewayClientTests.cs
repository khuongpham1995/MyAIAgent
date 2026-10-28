/**
 * MyAIAgent | Console Client · Tests
 *
 * Exercises Gateway wire contracts, bounded responses, HTTP failures, SSE framing, and cancellation.
 */

using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MyAIAgent.Gateway.Client;

namespace MyAIAgent.ConsoleClient.Tests;

public sealed class GatewayClientTests
{
    internal const string Session = "event: session\ndata: {\"type\":\"session\",\"sessionId\":\"s-1\"}\n\n";
    internal const string Delta = "event: delta\ndata: {\"type\":\"delta\",\"text\":\"Hello 🌏\\nRetrieved sources\\nhttps://example.com\"}\n\n";
    internal const string Done = "event: done\ndata: {\"type\":\"done\",\"sessionId\":\"s-1\"}\n\n";

    [Fact]
    public async Task StreamChatAsync_WhenFramesAreFragmented_ShouldPreserveTextAndDisposeBody()
    {
        // Arrange
        var body = new FragmentedReadStream(Encoding.UTF8.GetBytes((": heartbeat\n\n" + Session + Delta + Done).Replace("\n", "\r\n")));
        using var fixture = new GatewayFixture(_ => GatewayFixture.Stream(body));
        var events = new List<ChatStreamEvent>();

        // Act
        await foreach (var value in fixture.Client.StreamChatAsync(new ChatGatewayRequestDto { SessionId = null, UserMessage = "Hello", AgentId = "a-1" }, CancellationToken.None))
            events.Add(value);

        // Assert
        events.Select(value => value.Type).Should().Equal("session", "delta", "done");
        events[1].Text.Should().Be("Hello 🌏\nRetrieved sources\nhttps://example.com");
        body.WasDisposed.Should().BeTrue();
        fixture.Requests.Should().ContainSingle().Which.Accept.Should().Be("text/event-stream");
        fixture.Requests[0].Path.Should().Be("/chat/stream");
        using var json = JsonDocument.Parse(fixture.Requests[0].Body!);
        json.RootElement.GetProperty("agentId").GetString().Should().Be("a-1");
        json.RootElement.TryGetProperty("userId", out _).Should().BeFalse();
        fixture.Requests[0].HasAuthorization.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(Session)]
    [InlineData(Session + Delta)]
    [InlineData(Session + "event: delta\ndata: broken\n\n")]
    [InlineData(Session + "event: delta\ndata: {\"type\":\"done\",\"sessionId\":\"s-1\"}\n\n")]
    [InlineData(Delta + Done)]
    [InlineData(Session + "event: done\ndata: {\"type\":\"done\",\"sessionId\":\"s-2\"}\n\n")]
    public async Task StreamChatAsync_WhenMalformedOrIncomplete_ShouldNotSucceed(string stream)
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Stream(stream));

        // Act
        var act = async () => { await foreach (var item in fixture.Client.StreamChatAsync(new ChatGatewayRequestDto { SessionId = null, UserMessage = "Hello", AgentId = null }, CancellationToken.None)) { } };

        // Assert
        await act.Should().ThrowAsync<GatewayClientException>();
        fixture.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task StreamChatAsync_WhenErrorEvent_ShouldStopWithoutTreatingTrailingDoneAsSuccess()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Stream(Session + "event: error\ndata: {\"type\":\"error\",\"message\":\"Failed\"}\n\n" + Done));
        var types = new List<string>();

        // Act
        await foreach (var item in fixture.Client.StreamChatAsync(new ChatGatewayRequestDto { SessionId = null, UserMessage = "Hello", AgentId = null }, CancellationToken.None)) types.Add(item.Type);

        // Assert
        types.Should().Equal("session", "error");
    }

    [Fact]
    public async Task StreamChatAsync_WhenCallerCancelsBodyRead_ShouldPropagateAndDispose()
    {
        // Arrange
        var body = new BlockingReadStream();
        using var cancellation = new CancellationTokenSource();
        using var fixture = new GatewayFixture(_ => GatewayFixture.Stream(body));
        var task = ConsumeAsync(fixture.Client, cancellation.Token);
        await body.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        cancellation.Cancel();

        // Assert
        await FluentActions.Awaiting(() => task).Should().ThrowAsync<OperationCanceledException>();
        body.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task StreamChatAsync_WhenBodyStalls_ShouldApplyWholeOperationDeadline()
    {
        // Arrange
        var body = new BlockingReadStream();
        using var fixture = new GatewayFixture(_ => GatewayFixture.Stream(body), new ClientOptions(new Uri("https://localhost:7200")) with { ChatTimeout = TimeSpan.FromMilliseconds(100) });

        // Act
        var act = () => ConsumeAsync(fixture.Client, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        body.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task StreamChatAsync_WhenContentTypeIsJson_ShouldRejectWithoutParsingItAsEvents()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json("private-body"));

        // Act
        var act = () => ConsumeAsync(fixture.Client, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GatewayClientException>()).Which.Message.Should().Contain("event stream").And.NotContain("private-body");
    }

    [Fact]
    public async Task StreamChatAsync_WhenTitleArrives_ShouldPreserveItsPayload()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Stream(Session + Delta + "event: title\ndata: {\"type\":\"title\",\"title\":\"New title\"}\n\n" + Done));
        var events = new List<ChatStreamEvent>();

        // Act
        await foreach (var item in fixture.Client.StreamChatAsync(new ChatGatewayRequestDto { SessionId = null, UserMessage = "Hello", AgentId = null }, CancellationToken.None)) events.Add(item);

        // Assert
        events.Select(item => item.Type).Should().Equal("session", "delta", "title", "done");
        events[2].Title.Should().Be("New title");
    }

    [Fact]
    public async Task ListAgentsAsync_WhenBodyStalls_ShouldApplyRequestDeadlineAndDispose()
    {
        // Arrange
        var body = new BlockingReadStream();
        using var fixture = new GatewayFixture(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) },
            new ClientOptions(new Uri("https://localhost:7200")) with { RequestTimeout = TimeSpan.FromMilliseconds(100) });

        // Act
        var act = () => fixture.Client.ListAgentsAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        body.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task ChatAsync_WhenJsonSuccess_ShouldSendNormalContractAndReadReply()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json("""{"isSuccess":true,"data":{"reply":"Hello","sessionId":"s-1"}}"""));

        // Act
        var reply = await fixture.Client.ChatAsync(new ChatGatewayRequestDto { SessionId = "s-1", UserMessage = "Follow up", AgentId = null }, CancellationToken.None);

        // Assert
        reply.Should().BeEquivalentTo(new { Reply = "Hello", SessionId = "s-1" });
        fixture.Requests.Single().Path.Should().Be("/chat");
        fixture.Requests.Single().Body.Should().Contain("\"sessionId\":\"s-1\"");
    }

    [Fact]
    public async Task ChatAsync_WhenJsonHeadersAreDelayed_ShouldAllowTheChatDeadline()
    {
        // Arrange
        using var fixture = new GatewayFixture(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), token);
            return GatewayFixture.Json("""{"isSuccess":true,"data":{"reply":"Hello","sessionId":"s-1"}}""");
        }, new ClientOptions(new Uri("https://localhost:7200")) with { RequestTimeout = TimeSpan.FromMilliseconds(20), ChatTimeout = TimeSpan.FromSeconds(5) });

        // Act
        var reply = await fixture.Client.ChatAsync(new ChatGatewayRequestDto { UserMessage = "Hello" }, CancellationToken.None);

        // Assert
        reply.SessionId.Should().Be("s-1");
    }

    [Fact]
    public async Task StreamChatAsync_WhenHeadersStall_ShouldUseTheShortHeaderDeadline()
    {
        // Arrange
        using var fixture = new GatewayFixture(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return GatewayFixture.Stream("");
        }, new ClientOptions(new Uri("https://localhost:7200")) with { RequestTimeout = TimeSpan.FromMilliseconds(20), ChatTimeout = TimeSpan.FromSeconds(5) });

        // Act
        var act = () => ConsumeAsync(fixture.Client, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(400, "validation")]
    [InlineData(401, "denied")]
    [InlineData(403, "denied")]
    [InlineData(404, "not found")]
    [InlineData(429, "limit")]
    [InlineData(500, "failed")]
    [InlineData(302, "failed")]
    public async Task ListAgentsAsync_WhenHttpFails_ShouldGiveSafeErrorAndNeverRetry(int status, string expected)
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json("private-provider-body", (HttpStatusCode)status));

        // Act
        var act = () => fixture.Client.ListAgentsAsync(CancellationToken.None);

        // Assert
        var result = await act.Should().ThrowAsync<GatewayClientException>();
        result.Which.Message.Should().Contain(expected).And.NotContain("private-provider-body");
        fixture.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"isSuccess\":false,\"message\":\"private-provider-body\",\"data\":[]}")]
    public async Task ListAgentsAsync_WhenEnvelopeIsInvalid_ShouldFailSafely(string body)
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json(body));

        // Act
        var act = () => fixture.Client.ListAgentsAsync(CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GatewayClientException>()).Which.Message.Should().NotContain("private-provider-body");
    }

    [Theory]
    [InlineData("{\"isSuccess\":true,\"data\":[null]}")]
    [InlineData("{\"isSuccess\":true,\"data\":[{}]}")]
    [InlineData("{\"isSuccess\":true,\"data\":[{\"id\":\"../bad\",\"name\":\"Bad\"}]}")]
    [InlineData("{\"isSuccess\":true,\"data\":[{\"id\":\"a-1\",\"name\":\"Agent\",\"enabledTools\":null}]}")]
    public async Task ListAgentsAsync_WhenRequiredFieldsAreMissing_ShouldFailBeforeMenuRendering(string body)
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json(body));

        // Act
        var act = () => fixture.Client.ListAgentsAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<GatewayClientException>();
    }

    [Fact]
    public async Task ListToolsAsync_WhenEntryIsNull_ShouldFailBeforeMenuRendering()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json("""{"isSuccess":true,"data":[null]}"""));

        // Act
        var act = () => fixture.Client.ListToolsAsync(CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GatewayClientException>()).Which.Message.Should().Be("Gateway returned incomplete or invalid data.");
        fixture.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ListConversationsAsync_WhenEntryIsNull_ShouldFailBeforeMenuRendering()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json("""{"isSuccess":true,"data":[null]}"""));

        // Act
        var act = () => fixture.Client.ListConversationsAsync(1, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GatewayClientException>()).Which.Message.Should().Be("Gateway returned incomplete or invalid data.");
        fixture.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    public async Task GetConversationAsync_WhenMessagesContainNulls_ShouldFailBeforeMenuRendering(string messages)
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json("""{"isSuccess":true,"data":{"sessionId":"s-1","messages":""" + messages + "}}"));

        // Act
        var act = () => fixture.Client.GetConversationAsync("s-1", CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GatewayClientException>()).Which.Message.Should().Be("Gateway returned incomplete or invalid data.");
        fixture.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("[]", 0)]
    [InlineData("""[{"role":"assistant","content":"Hello"}]""", 1)]
    public async Task GetConversationAsync_WhenMessagesAreValid_ShouldPreserveHistory(string messages, int count)
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json("""{"isSuccess":true,"data":{"sessionId":"s-1","messages":""" + messages + "}}"));

        // Act
        var conversation = await fixture.Client.GetConversationAsync("s-1", CancellationToken.None);

        // Assert
        conversation.SessionId.Should().Be("s-1");
        conversation.Messages.Should().HaveCount(count);
        if (count > 0)
        {
            conversation.Messages[0].Role.Should().Be("assistant");
            conversation.Messages[0].Content.Should().Be("Hello");
        }
    }

    [Fact]
    public async Task ListToolsAsync_WhenResponseIsOversized_ShouldRejectBeforeParsingWholeBody()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json("{\"isSuccess\":true,\"data\":[],\"padding\":\"" + new string('x', 2 * 1024 * 1024) + "\"}"));

        // Act
        var act = () => fixture.Client.ListToolsAsync(CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GatewayClientException>()).Which.Message.Should().Contain("size limit");
    }

    [Fact]
    public async Task ListAgentsAsync_WhenContentLengthIsOversized_ShouldRejectBeforeReadingBody()
    {
        // Arrange
        var body = new BlockingReadStream();
        using var fixture = new GatewayFixture(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
            response.Content.Headers.ContentLength = 512 * 1024 * 1024;
            return response;
        });

        // Act
        var act = () => fixture.Client.ListAgentsAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<GatewayClientException>();
        body.Started.Task.IsCompleted.Should().BeFalse();
        body.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task ListAgentsAsync_WhenJsonIsMislabeledAsSse_ShouldStillEnforceJsonLimit()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ =>
        {
            var response = GatewayFixture.Json("{\"isSuccess\":true,\"data\":[],\"padding\":\"" + new string('x', 2 * 1024 * 1024) + "\"}");
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            return response;
        });

        // Act
        var act = () => fixture.Client.ListAgentsAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<GatewayClientException>();
    }

    [Fact]
    public async Task RenameConversationAsync_WhenServerReturnsUnexpectedSuccessCode_ShouldNotClaimSuccess()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json("private-body"));

        // Act
        var act = () => fixture.Client.RenameConversationAsync("s-1", "Title", CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<GatewayClientException>()).Which.Message.Should().NotContain("private-body");
        fixture.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ConversationMutationsAsync_WhenNoContent_ShouldUseEscapedRoutesAndTitleBody()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        // Act
        await fixture.Client.RenameConversationAsync("s-1", "New title", CancellationToken.None);
        await fixture.Client.DeleteConversationAsync("s-1", CancellationToken.None);

        // Assert
        fixture.Requests.Select(request => request.Method).Should().Equal("PUT", "DELETE");
        fixture.Requests.Select(request => request.Path).Should().Equal("/conversations/s-1/title", "/conversations/s-1");
        fixture.Requests[0].Body.Should().Be("{\"title\":\"New title\"}");
        fixture.Requests[1].Body.Should().BeNull();
    }

    private static async Task ConsumeAsync(GatewayClient client, CancellationToken cancellationToken)
    {
        await foreach (var item in client.StreamChatAsync(new ChatGatewayRequestDto { SessionId = null, UserMessage = "Hello", AgentId = null }, cancellationToken)) { }
    }
}
