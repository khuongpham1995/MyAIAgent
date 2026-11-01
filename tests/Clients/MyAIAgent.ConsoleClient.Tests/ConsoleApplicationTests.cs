/**
 * MyAIAgent | Console Client · Tests
 *
 * Exercises interactive journeys with the real client, scripted terminal input, and mocked HTTP responses.
 */

using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace MyAIAgent.ConsoleClient.Tests;

public sealed class ConsoleApplicationTests
{
    private const string Agents = """{"isSuccess":true,"data":[{"id":"a-1","name":"Research","enabledTools":["web_search","read_webpage"]}]}""";
    private const string Tools = """{"isSuccess":true,"data":[{"name":"web_search","description":"Search sources"},{"name":"read_webpage","description":"Read a page"}]}""";
    private const string Conversations = """{"isSuccess":true,"data":[{"sessionId":"s-1","title":"Saved chat","updatedAt":"2026-01-01T00:00:00Z"}]}""";
    private const string Detail = """{"isSuccess":true,"data":{"sessionId":"s-1","title":"Saved chat","messages":[{"role":"user","content":"Original question","createdAt":"2026-01-01T00:00:00Z"},{"role":"assistant","content":"Original answer","createdAt":"2026-01-01T00:00:01Z"}]}}""";

    [Fact]
    public async Task RunAsync_WhenAgentSelectedAndChatContinues_ShouldReuseSessionAndPrintSources()
    {
        // Arrange
        using var fixture = StandardFixture();
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("3\n1\n1\nHello\nFollow up\n/back\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        var chats = fixture.Requests.Where(request => request.Path == "/sse/chat").ToArray();
        chats.Should().HaveCount(2);
        using var first = JsonDocument.Parse(chats[0].Body!);
        using var second = JsonDocument.Parse(chats[1].Body!);
        first.RootElement.GetProperty("sessionId").ValueKind.Should().Be(JsonValueKind.Null);
        second.RootElement.GetProperty("sessionId").GetString().Should().Be("s-1");
        second.RootElement.GetProperty("agentId").GetString().Should().Be("a-1");
        output.ToString().Should().Contain("Hello 🌏").And.Contain("Retrieved sources").And.Contain("Completed | Session: s-1");
    }

    [Fact]
    public async Task RunAsync_WhenOpeningSavedChat_ShouldDisplayHistoryAndPreserveServerAgent()
    {
        // Arrange
        using var fixture = StandardFixture();
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("3\n1\n2\n1\no\nContinue\n/back\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        var chat = fixture.Requests.Single(request => request.Path == "/sse/chat");
        using var body = JsonDocument.Parse(chat.Body!);
        body.RootElement.GetProperty("sessionId").GetString().Should().Be("s-1");
        body.RootElement.GetProperty("agentId").ValueKind.Should().Be(JsonValueKind.Null);
        output.ToString().Should().Contain("user: Original question").And.Contain("assistant: Original answer");
    }

    [Fact]
    public async Task RunAsync_WhenPagingHistory_ShouldRequestNextAndPreviousPages()
    {
        // Arrange
        var entries = Enumerable.Range(1, 20).Select(index => new { sessionId = $"s-{index}", title = $"Chat {index}", updatedAt = DateTimeOffset.UnixEpoch });
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json(JsonSerializer.Serialize(new { isSuccess = true, data = entries })));
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("2\nn\np\n\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        fixture.Requests.Select(request => request.Path).Should().Equal("/api/conversations?page=1&pageSize=20", "/api/conversations?page=2&pageSize=20", "/api/conversations?page=1&pageSize=20");
    }

    [Fact]
    public async Task RunAsync_WhenAgentSelectedAfterSavedChat_ShouldStartNewSession()
    {
        // Arrange
        using var fixture = StandardFixture();
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("2\n1\no\n/back\n3\n1\n6\nHello\n/back\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        using var body = JsonDocument.Parse(fixture.Requests.Single(request => request.Path == "/sse/chat").Body!);
        body.RootElement.GetProperty("sessionId").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("agentId").GetString().Should().Be("a-1");
    }

    [Fact]
    public async Task RunAsync_WhenJsonModeSelected_ShouldUseNonstreamingChat()
    {
        // Arrange
        using var fixture = StandardFixture();
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("7\n1\nHello\n/back\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        fixture.Requests.Should().ContainSingle().Which.Path.Should().Be("/api/chat");
        output.ToString().Should().Contain("Mode: JSON").And.Contain("JSON reply");
    }

    [Fact]
    public async Task RunAsync_WhenStreamFails_ShouldKeepSessionWithoutClaimingCompletionOrRetrying()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Stream(GatewayClientTests.Session + GatewayClientTests.Delta));
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("1\nHello\n/back\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        output.ToString().Should().Contain("Reply incomplete").And.Contain("Session: s-1").And.NotContain("Completed | Session");
        fixture.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task RunAsync_WhenServerSendsError_ShouldNotDisplayRawErrorDetailsOrCompletion()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Stream(GatewayClientTests.Session + "event: error\ndata: {\"type\":\"error\",\"message\":\"private-provider-body\"}\n\n"));
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("1\nHello\n/back\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        output.ToString().Should().Contain("Chat failed").And.NotContain("private-provider-body").And.NotContain("Completed | Session");
    }

    [Fact]
    public async Task RunAsync_WhenUserCancelsStream_ShouldReturnToPromptAndDisposeBody()
    {
        // Arrange
        var body = new BlockingReadStream();
        using var fixture = new GatewayFixture(_ => GatewayFixture.Stream(body));
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("1\nHello\n/back\n0\n"), output, fixture.Options);
        var task = app.RunAsync();
        await body.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        app.Cancel();
        await task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        body.WasDisposed.Should().BeTrue();
        output.ToString().Should().Contain("Operation cancelled").And.Contain("Reply incomplete").And.NotContain("Completed | Session");
        fixture.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("no", 0)]
    [InlineData("yes", 1)]
    public async Task RunAsync_WhenDeletingConversation_ShouldRequireExplicitConfirmation(string answer, int expectedDeletes)
    {
        // Arrange
        using var fixture = StandardFixture();
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader($"2\n1\nd\n{answer}\n\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        fixture.Requests.Count(request => request.Method == "DELETE").Should().Be(expectedDeletes);
    }

    [Fact]
    public async Task RunAsync_WhenRenamingConversation_ShouldSendTitleAndReturnToList()
    {
        // Arrange
        using var fixture = StandardFixture();
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("2\n1\nr\nNew title\n\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        fixture.Requests.Single(request => request.Method == "PUT").Body.Should().Be("{\"title\":\"New title\"}");
        output.ToString().Should().Contain("Conversation renamed");
    }

    [Fact]
    public async Task RunAsync_WhenCreatingAgent_ShouldSendOnlyExplicitToolsAndNeverSetDefault()
    {
        // Arrange
        using var fixture = StandardFixture(new ClientOptions(new Uri("https://localhost:7200"))
        {
            McpServerEndpoint = new Uri("http://localhost:5555/custom-mcp"),
        });
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("3\nc\nResearch\nCheck current sources\nweb_search,read_webpage\n\nyes\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        using var body = JsonDocument.Parse(fixture.Requests.Single(request => request.Method == "POST").Body!);
        body.RootElement.GetProperty("isDefault").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("enabledTools").EnumerateArray().Select(value => value.GetString()).Should().Equal("web_search", "read_webpage");
        body.RootElement.GetProperty("mcpServerEndpoint").GetString().Should().Be("http://localhost:5555/custom-mcp");
        output.ToString().Should().Contain("Agent created: a-new");
    }

    [Theory]
    [InlineData("unknown", "")]
    [InlineData("web_search", "\nno\n")]
    public async Task RunAsync_WhenAgentInputIsUnknownOrUnconfirmed_ShouldNotCreate(string selection, string remainder)
    {
        // Arrange
        using var fixture = StandardFixture();
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader($"3\nc\nResearch\nInstructions\n{selection}\n{remainder}0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        fixture.Requests.Should().NotContain(request => request.Method == "POST");
    }

    [Fact]
    public async Task RunAsync_WhenMcpEndpointUnsetAndInputEmpty_ShouldNotCreateAgentWithFallback()
    {
        // Arrange
        using var fixture = StandardFixture(new ClientOptions(new Uri("https://localhost:7200")));
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client,
            new StringReader("3\nc\nResearch\nInstructions\nweb_search\n\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        fixture.Requests.Should().NotContain(request => request.Method == "POST");
        output.ToString().Should().Contain("Invalid MCP endpoint");
    }

    [Fact]
    public async Task RunAsync_WhenListsAreEmpty_ShouldExplainHowToContinueWithoutCreatingAnything()
    {
        // Arrange
        using var fixture = new GatewayFixture(_ => GatewayFixture.Json("""{"isSuccess":true,"data":[]}"""));
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("3\n\n2\n\n4\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        output.ToString().Should().Contain("No agents exist").And.Contain("No conversations").And.Contain("No tools discovered");
        fixture.Requests.Should().OnlyContain(request => request.Method == "GET");
    }

    [Fact]
    public async Task RunAsync_WhenInputIsEmptyOrOversized_ShouldRejectWithoutHttpCalls()
    {
        // Arrange
        using var fixture = StandardFixture();
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("1\n\n" + new string('x', 2001) + "\n/back\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        fixture.Requests.Should().BeEmpty();
        output.ToString().Should().Contain("at most 2000 characters");
    }

    [Fact]
    public async Task RunAsync_WhenProviderTextContainsTerminalEscapes_ShouldPrintSafeTextOnly()
    {
        // Arrange
        const string injected = "event: delta\ndata: {\"type\":\"delta\",\"text\":\"safe\\u001b]52;c;\"}\n\nevent: delta\ndata: {\"type\":\"delta\",\"text\":\"clipboard\\u0007tail\"}\n\n";
        using var fixture = new GatewayFixture(_ => GatewayFixture.Stream(GatewayClientTests.Session + injected + GatewayClientTests.Done));
        await using var output = new StringWriter();
        using var app = new ConsoleApplication(fixture.Client, new StringReader("1\nHello\n/back\n0\n"), output, fixture.Options);

        // Act
        await app.RunAsync();

        // Assert
        output.ToString().Should().Contain("safetail").And.NotContain("clipboard").And.NotContain("\u001b");
    }

    private static GatewayFixture StandardFixture(ClientOptions? options = null) => new(request => request.RequestUri!.AbsolutePath switch
    {
        "/api/admin/agents" when request.Method == HttpMethod.Post => GatewayFixture.Json("""{"isSuccess":true,"data":"a-new"}""", HttpStatusCode.Created),
        "/api/admin/agents" => GatewayFixture.Json(Agents),
        "/api/admin/tools" => GatewayFixture.Json(Tools),
        "/api/conversations" => GatewayFixture.Json(Conversations),
        "/api/conversations/s-1" when request.Method == HttpMethod.Get => GatewayFixture.Json(Detail),
        "/api/conversations/s-1" or "/api/conversations/s-1/title" => new HttpResponseMessage(HttpStatusCode.NoContent),
        "/api/chat" => GatewayFixture.Json("""{"isSuccess":true,"data":{"reply":"JSON reply","sessionId":"s-1"}}"""),
        "/sse/chat" => GatewayFixture.Stream(GatewayClientTests.Session + GatewayClientTests.Delta + GatewayClientTests.Done),
        _ => new HttpResponseMessage(HttpStatusCode.NotFound),
    }, options);
}
