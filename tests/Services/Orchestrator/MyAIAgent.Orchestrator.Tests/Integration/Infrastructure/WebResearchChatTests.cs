/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Exercises real MCP discovery, function invocation, source collection, and persisted ordinary or streamed replies with mocked I/O.
 */

using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Common.Infrastructure.Http;
using MyAIAgent.Orchestrator.Application.Chat;
using MyAIAgent.Orchestrator.Application.Chat.Commands.Chat;
using MyAIAgent.Orchestrator.Application.Chat.Commands.StreamChat;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Infrastructure.Helpers;
using MyAIAgent.Orchestrator.Infrastructure.Services;
using AiMessage = Microsoft.Extensions.AI.ChatMessage;
using AiResponse = Microsoft.Extensions.AI.ChatResponse;

namespace MyAIAgent.Orchestrator.Tests.Integration.Infrastructure;

public sealed class WebResearchChatTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task Chat_WhenModelRequestsMoreThanThreeWebCalls_ShouldBoundOnlyFirstPartyInvocations(bool streaming, bool trusted)
    {
        // Arrange
        using var fixture = new Fixture(messages =>
        {
            var results = messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToList();
            if (results.Count < 5) return Call("web_search", "query", "topic");
            if (trusted) results.Skip(3).Should().OnlyContain(result => result.Result!.ToString()!.Contains("limit reached"));
            return new AiResponse(new AiMessage(ChatRole.Assistant, "Answer with available evidence."));
        }, trusted: trusted);

        // Act
        var answer = await Answer(fixture.Service, streaming);

        // Assert
        fixture.Wire.Calls.Should().HaveCount(trusted ? 3 : 5).And.OnlyContain(name => name == "web_search");
        answer.Should().Contain("Answer with available evidence.");
        answer.Contains("Retrieved sources").Should().Be(trusted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chat_WhenSearchAndReadComplete_ShouldDeliverAndPersistOneSourceFooter(bool streaming)
    {
        // Arrange
        using var fixture = new Fixture(messages => ResearchResponse(messages, "A concise answer without a model citation."));
        ChatSession? saved = null;
        var sessions = new Mock<IChatSessionRepository>();
        sessions.Setup(repository => repository.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Callback<ChatSession, CancellationToken>((session, _) => saved = session).Returns(Task.CompletedTask);
        var work = new Mock<IUnitOfWork>();
        work.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var summary = Mock.Of<IConversationSummarizer>();
        var title = new Mock<IConversationTitleGenerator>();
        title.Setup(generator => generator.GenerateTitleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("Research");
        var events = new List<ChatStreamEvent>();

        // Act
        string answer;
        if (streaming)
        {
            var handler = new StreamChatHandler(fixture.Service, sessions.Object, work.Object, title.Object, summary, TimeProvider.System, NullLogger<StreamChatHandler>.Instance);
            await foreach (var item in handler.Handle(new StreamChatCommand(null, "Research this topic", "test-user"), CancellationToken.None)) events.Add(item);
            answer = string.Concat(events.Where(item => item.Type == "delta").Select(item => item.Text));
        }
        else
        {
            var handler = new ChatHandler(fixture.Service, sessions.Object, work.Object, summary, TimeProvider.System, NullLogger<ChatHandler>.Instance);
            answer = (await handler.Handle(new ChatCommand(null, "Research this topic", "test-user"), CancellationToken.None)).Data!.Reply;
        }

        // Assert
        answer.Should().Be("A concise answer without a model citation.\n\n**Retrieved sources**\n- <https://example.com/source>\n");
        saved!.Messages.Last().Content.Should().Be(answer);
        fixture.Wire.Calls.Should().Equal("web_search", "read_webpage");
        if (streaming) events.Last().Type.Should().Be("done");
        work.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chat_WhenExternalToolUsesWebName_ShouldNotCollectOrApplyFirstPartyBudget(bool streaming)
    {
        // Arrange
        using var fixture = new Fixture(messages => ResearchResponse(messages, "Answer"), trusted: false);

        // Act
        var answer = await Answer(fixture.Service, streaming);

        // Assert
        answer.Should().Be("Answer");
        fixture.Wire.Calls.Should().Equal("web_search", "read_webpage");
        fixture.Wire.Authorization.Should().OnlyContain(header => header == null);
    }

    [Theory]
    [InlineData(false, "empty")]
    [InlineData(true, "empty")]
    [InlineData(false, "failure")]
    [InlineData(true, "failure")]
    [InlineData(false, "cancel")]
    [InlineData(true, "cancel")]
    public async Task Chat_WhenGenerationDoesNotComplete_ShouldNotAppendCollectedSources(bool streaming, string outcome)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        using var fixture = new Fixture(messages =>
        {
            if (!messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any())
                return Call("web_search", "query", "topic");
            if (outcome == "failure") throw new HttpRequestException("Provider unavailable");
            if (outcome == "cancel") cancellation.Cancel();
            return new AiResponse(new AiMessage(ChatRole.Assistant, ""));
        });
        var chunks = new List<string>();

        // Act
        if (outcome == "cancel")
        {
            var action = () => Answer(fixture.Service, streaming, cancellation.Token, chunks);
            await action.Should().ThrowAsync<OperationCanceledException>();
        }
        else if (streaming && outcome == "failure")
        {
            var action = () => Answer(fixture.Service, true, cancellation.Token, chunks);
            await action.Should().ThrowAsync<HttpRequestException>();
        }
        else
        {
            var answer = await Answer(fixture.Service, streaming, cancellation.Token, chunks);
            answer.Should().BeEmpty();
        }

        // Assert
        string.Concat(chunks).Should().NotContain("Retrieved sources");
        fixture.Wire.Calls.Should().Equal("web_search");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chat_WhenToolReturnsError_ShouldNotCollectItsSourceLookingText(bool streaming)
    {
        // Arrange
        using var fixture = new Fixture(messages => ResearchResponse(messages, "Evidence unavailable."), toolError: true);

        // Act
        var answer = await Answer(fixture.Service, streaming);

        // Assert
        answer.Should().Be("Evidence unavailable.");
        fixture.Wire.Calls.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Chat_WhenTurnsRunConcurrently_ShouldKeepSourcesIsolated()
    {
        // Arrange
        using var fixture = new Fixture(messages =>
        {
            var topic = messages.First(message => message.Role == ChatRole.User).Text;
            return messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any()
                ? new AiResponse(new AiMessage(ChatRole.Assistant, "Answer " + topic))
                : Call("web_search", "query", topic);
        });

        // Act
        var answers = await Task.WhenAll(new[] { "alpha", "beta" }.Select(async topic =>
            (await fixture.Service.ChatAsync(new ChatRequest(topic, []), CancellationToken.None)).Data!));

        // Assert
        answers[0].Should().Contain("/alpha>").And.NotContain("/beta>");
        answers[1].Should().Contain("/beta>").And.NotContain("/alpha>");
    }

    private static AiResponse ResearchResponse(IEnumerable<AiMessage> messages, string answer)
    {
        var results = messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Count();
        return results switch
        {
            0 => Call("web_search", "query", "topic"),
            1 => Call("read_webpage", "url", "https://example.com/source"),
            _ => new AiResponse(new AiMessage(ChatRole.Assistant, answer))
        };
    }

    private static AiResponse Call(string name, string argument, string value) => new(new AiMessage(ChatRole.Assistant,
        [new FunctionCallContent(Guid.NewGuid().ToString(), name, new Dictionary<string, object?> { [argument] = value })]));

    private static async Task<string> Answer(ChatOrchestratorService service, bool streaming, CancellationToken cancellationToken = default, List<string>? chunks = null)
    {
        if (!streaming) return (await service.ChatAsync(new ChatRequest("topic", []), cancellationToken)).Data ?? string.Empty;
        chunks ??= [];
        await foreach (var chunk in service.StreamChatAsync(new ChatRequest("topic", []), cancellationToken)) chunks.Add(chunk);
        return string.Concat(chunks);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient http;
        private readonly IChatClient client;
        public ResearchWire Wire { get; }
        public ChatOrchestratorService Service { get; }

        public Fixture(Func<IEnumerable<AiMessage>, AiResponse> respond, bool trusted = true, bool toolError = false)
        {
            Wire = new ResearchWire(toolError);
            http = new HttpClient(Wire);
            var model = new Mock<IChatClient>();
            model.Setup(model => model.GetResponseAsync(It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<AiMessage> messages, ChatOptions? _, CancellationToken _) => Task.FromResult(respond(messages)));
            model.Setup(model => model.GetStreamingResponseAsync(It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<AiMessage> messages, ChatOptions? _, CancellationToken token) => Updates(messages, respond, token));
            client = new ChatClientBuilder(model.Object).UseFunctionInvocation().Build();
            var settings = McpServiceClientSettings.Load(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpServer:BaseUrl"] = "https://mcp.example",
                ["McpServer:AccessKey"] = "test-key",
                ["McpServer:AllowedExternalOrigins:0"] = "https://external.example"
            }).Build(), false);
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(factory => factory.CreateClient(AgentHelper.ExternalClientName)).Returns(http);
            var helper = new AgentHelper(http, NullLogger<AgentHelper>.Instance, settings, factory.Object);
            var agent = new AgentDefinition
            {
                McpServerEndpoint = trusted ? "https://mcp.example/mcp" : "https://external.example/mcp",
                AgentTools = [new AgentTool { Name = "web_search", IsEnabled = true }, new AgentTool { Name = "read_webpage", IsEnabled = true }]
            };
            var repository = new Mock<IAgentRepository>();
            repository.Setup(repository => repository.FindDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(agent);
            Service = new ChatOrchestratorService(client, repository.Object, helper, NullLogger<ChatOrchestratorService>.Instance, TimeProvider.System);
        }

        public void Dispose() { client.Dispose(); http.Dispose(); }

        private static async IAsyncEnumerable<ChatResponseUpdate> Updates(IEnumerable<AiMessage> messages,
            Func<IEnumerable<AiMessage>, AiResponse> respond, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var update in respond(messages).ToChatResponseUpdates()) yield return update;
        }
    }

    private sealed class ResearchWire(bool toolError) : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];
        public List<string?> Authorization { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Authorization) Authorization.Add(request.Headers.Authorization?.ToString());
            if (request.Method != HttpMethod.Post) return new(HttpStatusCode.MethodNotAllowed);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var root = json.RootElement;
            if (!root.TryGetProperty("id", out var id)) return new(HttpStatusCode.Accepted);
            var method = root.GetProperty("method").GetString();
            if (method == "server/discover") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { jsonrpc = "2.0", id = id.Clone(), error = new { code = -32601, message = "Not supported" } }) };
            object result;
            if (method == "initialize") result = new { protocolVersion = root.GetProperty("params").GetProperty("protocolVersion").GetString(), capabilities = new { tools = new { } }, serverInfo = new { name = "research-test", version = "1" } };
            else if (method == "tools/list") result = new
            {
                tools = new[] { "web_search", "read_webpage" }.Select(name => new { name, inputSchema = new { type = "object", properties = new Dictionary<string, object> { [name == "web_search" ? "query" : "url"] = new { type = "string" } } } }).ToArray()
            };
            else if (method == "tools/call")
            {
                var name = root.GetProperty("params").GetProperty("name").GetString()!;
                lock (Calls) Calls.Add(name);
                var topic = name == "web_search" ? root.GetProperty("params").GetProperty("arguments").GetProperty("query").GetString() : "topic";
                var url = "https://example.com/" + (topic is "alpha" or "beta" ? topic : "source");
                var text = name == "web_search" ? JsonSerializer.Serialize(new[] { new { Url = url, Content = "Excerpt" } }) : JsonSerializer.Serialize(new { Url = url, Content = "Article" });
                result = new { content = new[] { new { type = "text", text } }, isError = toolError };
            }
            else throw new InvalidOperationException("Unexpected MCP method " + method);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { jsonrpc = "2.0", id = id.Clone(), result }) };
        }
    }
}
