/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Exercises real function invocation during ordinary and streaming chat with scripted model replies and search evidence.
 */

using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Orchestrator.Application.Chat;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Infrastructure.Helpers;
using MyAIAgent.Orchestrator.Infrastructure.Services;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatResponse = Microsoft.Extensions.AI.ChatResponse;

namespace MyAIAgent.Orchestrator.Tests.Integration.Infrastructure;

public sealed class AutomaticWebSearchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chat_WhenModelRequestsSearch_ShouldInvokeToolAndContinueWithEvidence(bool streaming)
    {
        // Arrange
        const string evidence = "[{\"Url\":\"https://example.com/source\",\"Content\":\"Current release evidence\"}]";
        var calls = new List<string>();
        var tool = AIFunctionFactory.Create((string query) => { calls.Add(query); return evidence; }, "web_search");
        var model = new Mock<IChatClient>();
        List<ChatMessage>? initialMessages = null;
        var turns = 0;
        ChatResponse Respond(IEnumerable<ChatMessage> messages)
        {
            turns++;
            var list = messages.ToList();
            initialMessages ??= list;
            var result = list.SelectMany(m => m.Contents).OfType<FunctionResultContent>().LastOrDefault();
            if (result is not null)
            {
                result.Result!.ToString().Should().Contain("Current release evidence").And.Contain("https://example.com/source");
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "The release is current [source](https://example.com/source)."));
            }
            return new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("call-1", "web_search", new Dictionary<string, object?> { ["query"] = "current release" })]));
        }
        model.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) => Task.FromResult(Respond(messages)));
        model.Setup(c => c.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) => Updates(Respond(messages)));
        using var client = new ChatClientBuilder(model.Object).UseFunctionInvocation().Build();
        var service = Service(client, [tool]);
        var request = new ChatRequest("What is the current release?", []);

        // Act
        var answer = streaming ? await Stream(service, request) : (await service.ChatAsync(request, CancellationToken.None)).Data;

        // Assert
        answer.Should().Contain("https://example.com/source");
        calls.Should().Equal("current release");
        turns.Should().Be(2);
        var guidance = string.Join(" ", initialMessages!.Where(m => m.Role == ChatRole.System).Select(m => m.Text));
        guidance.Should().Contain("2026-10-21").And.Contain("untrusted").And.Contain("Cite").And.Contain("If search fails");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chat_WhenModelDoesNotRequestSearch_ShouldReturnWithoutCallingTool(bool streaming)
    {
        // Arrange
        var calls = 0;
        var tool = AIFunctionFactory.Create((string query) => { calls++; return "[]"; }, "web_search");
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello."));
        var model = new Mock<IChatClient>();
        model.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
        model.Setup(c => c.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>())).Returns(Updates(response));
        using var client = new ChatClientBuilder(model.Object).UseFunctionInvocation().Build();
        var service = Service(client, [tool]);

        // Act
        var answer = streaming ? await Stream(service, new ChatRequest("Hi", [])) : (await service.ChatAsync(new ChatRequest("Hi", []), CancellationToken.None)).Data;

        // Assert
        answer.Should().Be("Hello.");
        calls.Should().Be(0);
    }

    [Fact]
    public async Task Chat_WhenSearchToolIsNotExposed_ShouldOmitSearchGuidance()
    {
        // Arrange
        var model = new Mock<IChatClient>();
        List<ChatMessage>? captured = null;
        model.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) => captured = messages.ToList())
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello.")));
        var service = Service(model.Object, []);

        // Act
        await service.ChatAsync(new ChatRequest("Hi", []), CancellationToken.None);

        // Assert
        captured.Should().NotContain(m => m.Text.Contains("web_search"));
        captured.Should().Contain(m => m.Role == ChatRole.System && m.Text.Contains("Current UTC date/time: 2026-10-21T00:00:00.0000000+00:00"));
    }

    private static ChatOrchestratorService Service(IChatClient client, IReadOnlyList<AITool> tools)
    {
        var agent = new AgentDefinition { Name = "Default", IsDefault = true, Instructions = "Answer clearly." };
        var repository = new Mock<IAgentRepository>();
        repository.Setup(r => r.FindDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var helper = new Mock<IAgentHelper>();
        helper.Setup(h => h.GetAgentToolsAsync(agent, It.IsAny<CancellationToken>())).ReturnsAsync(new AgentToolSet(tools));
        return new ChatOrchestratorService(client, repository.Object, helper.Object, NullLogger<ChatOrchestratorService>.Instance, new FixedClock());
    }

    private static async Task<string> Stream(ChatOrchestratorService service, ChatRequest request)
    {
        var chunks = new List<string>();
        await foreach (var chunk in service.StreamChatAsync(request, CancellationToken.None)) chunks.Add(chunk);
        return string.Concat(chunks);
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Updates(ChatResponse response)
    {
        foreach (var update in response.ToChatResponseUpdates())
        {
            await Task.Yield();
            yield return update;
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
    }
}
