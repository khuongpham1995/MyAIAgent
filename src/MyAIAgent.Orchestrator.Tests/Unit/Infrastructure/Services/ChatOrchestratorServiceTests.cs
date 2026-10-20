/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Checks agent selection, tool availability, provider failures, streaming output, and transport disposal.
 */

using System.ClientModel;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatResponse = Microsoft.Extensions.AI.ChatResponse;
using Moq;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Application.Common.Exceptions;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;
using MyAIAgent.Orchestrator.Application.Chat;
using MyAIAgent.Orchestrator.Infrastructure.Helpers;
using MyAIAgent.Orchestrator.Infrastructure.Services;

namespace MyAIAgent.Orchestrator.Tests.Unit.Infrastructure.Services;

public sealed class ChatOrchestratorServiceTests
{
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task ChatAsync_WhenMcpAuthenticationFails_ShouldReturnSafeFailureWithoutCallingModel(int status)
    {
        // Arrange
        var client = new Mock<IChatClient>();
        var repository = new Mock<IAgentRepository>();
        var helper = new Mock<IAgentHelper>();
        var agent = new AgentDefinition();
        repository.Setup(r => r.FindDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        helper.Setup(h => h.GetAgentToolsAsync(agent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("MCP service authentication failed.", null, (System.Net.HttpStatusCode)status));
        var service = BuildService(client, repository, helper);

        // Act
        var result = await service.ChatAsync(new ChatRequest("Hi", []), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("AI service temporarily unavailable.");
        client.Invocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task StreamChatAsync_WhenMcpAuthenticationFails_ShouldPropagateBeforeCallingModel(int status)
    {
        // Arrange
        var client = new Mock<IChatClient>();
        var repository = new Mock<IAgentRepository>();
        var helper = new Mock<IAgentHelper>();
        var agent = new AgentDefinition();
        repository.Setup(r => r.FindDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        helper.Setup(h => h.GetAgentToolsAsync(agent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("MCP service authentication failed.", null, (System.Net.HttpStatusCode)status));
        var service = BuildService(client, repository, helper);

        // Act
        var action = async () =>
        {
            await foreach (var _ in service.StreamChatAsync(new ChatRequest("Hi", []), CancellationToken.None)) { }
        };

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
        client.Invocations.Should().BeEmpty();
    }

    private static ChatOrchestratorService BuildService(
        Mock<IChatClient>? chatClient = null,
        Mock<IAgentRepository>? agentRepo = null,
        Mock<IAgentHelper>? agentBuilder = null)
    {
        return new ChatOrchestratorService(
            (chatClient ?? new Mock<IChatClient>()).Object,
            (agentRepo ?? new Mock<IAgentRepository>()).Object,
            (agentBuilder ?? new Mock<IAgentHelper>()).Object,
            NullLogger<ChatOrchestratorService>.Instance);
    }

    [Fact]
    public async Task ChatAsync_WhenNoAgentFound_ShouldReturnReplyWithoutTools()
    {
        // Arrange
        var mockClient = new Mock<IChatClient>();
        var mockAgentRepo = new Mock<IAgentRepository>();
        var mockAgentBuilder = new Mock<IAgentHelper>();

        mockAgentRepo.Setup(r => r.FindDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinition?)null);

        mockClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello!")));

        var service = BuildService(mockClient, mockAgentRepo, mockAgentBuilder);
        var request = new ChatRequest("Hi", []);

        // Act
        var result = await service.ChatAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("Hello!");
        mockAgentBuilder.Verify(
            b => b.GetAgentToolsAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChatAsync_WhenDefaultAgentFound_ShouldPassToolsToChatClient()
    {
        // Arrange
        var mockClient = new Mock<IChatClient>();
        var mockAgentRepo = new Mock<IAgentRepository>();
        var mockAgentBuilder = new Mock<IAgentHelper>();

        var agent = new AgentDefinition { Name = "Default", IsDefault = true };
        var fakeTools = new List<AITool>();

        mockAgentRepo.Setup(r => r.FindDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        mockAgentBuilder.Setup(b => b.GetAgentToolsAsync(agent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolSet(fakeTools));

        ChatOptions? capturedOptions = null;
        mockClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>(
                (_, opts, _) => capturedOptions = opts)
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Reply")));

        var service = BuildService(mockClient, mockAgentRepo, mockAgentBuilder);
        var request = new ChatRequest("Question", []);

        // Act
        var result = await service.ChatAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedOptions.Should().NotBeNull();
        mockAgentBuilder.Verify(
            b => b.GetAgentToolsAsync(agent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChatAsync_WhenSpecificAgentIdProvided_ShouldLookUpById()
    {
        // Arrange
        var mockClient = new Mock<IChatClient>();
        var mockAgentRepo = new Mock<IAgentRepository>();
        var mockAgentBuilder = new Mock<IAgentHelper>();

        var agent = new AgentDefinition { Id = "agent-99", Name = "Specific" };

        mockAgentRepo.Setup(r => r.FindByIdAsync("agent-99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        mockAgentBuilder.Setup(b => b.GetAgentToolsAsync(agent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentToolSet.Empty);

        mockClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Done")));

        var service = BuildService(mockClient, mockAgentRepo, mockAgentBuilder);
        var request = new ChatRequest("Hi", [], AgentId: "agent-99");

        // Act
        await service.ChatAsync(request, CancellationToken.None);

        // Assert
        mockAgentRepo.Verify(r => r.FindByIdAsync("agent-99", It.IsAny<CancellationToken>()), Times.Once);
        mockAgentRepo.Verify(r => r.FindDefaultAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChatAsync_WhenHttpRequestFails_ShouldReturnFailure()
    {
        // Arrange
        var mockClient = new Mock<IChatClient>();
        var mockAgentRepo = new Mock<IAgentRepository>();

        mockAgentRepo.Setup(r => r.FindDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinition?)null);

        mockClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var service = BuildService(mockClient, mockAgentRepo);
        var request = new ChatRequest("Hi", []);

        // Act
        var result = await service.ChatAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("AI service temporarily unavailable.");
    }

    [Fact]
    public async Task StreamChatAsync_WhenProviderStreams_ShouldYieldNonEmptyTextChunks()
    {
        // Arrange
        var mockClient = new Mock<IChatClient>();
        var mockAgentRepo = new Mock<IAgentRepository>();
        mockAgentRepo.Setup(r => r.FindDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinition?)null);
        mockClient
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToUpdates("Hel", "", "lo"));
        var service = BuildService(mockClient, mockAgentRepo);

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in service.StreamChatAsync(new ChatRequest("Hi", []), CancellationToken.None))
            chunks.Add(chunk);

        // Assert
        chunks.Should().Equal("Hel", "lo");
    }

    [Fact]
    public async Task StreamChatAsync_WhenStreamEnds_ShouldReleaseAgentTools()
    {
        // Arrange
        var mockClient = new Mock<IChatClient>();
        var mockAgentRepo = new Mock<IAgentRepository>();
        var mockAgentBuilder = new Mock<IAgentHelper>();
        var agent = new AgentDefinition { Name = "Default", IsDefault = true };
        var resource = new Mock<IAsyncDisposable>();
        mockAgentRepo.Setup(r => r.FindDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        mockAgentBuilder.Setup(b => b.GetAgentToolsAsync(agent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentToolSet([], resource.Object));
        mockClient
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToUpdates("done"));
        var service = BuildService(mockClient, mockAgentRepo, mockAgentBuilder);

        // Act
        await foreach (var _ in service.StreamChatAsync(new ChatRequest("Hi", []), CancellationToken.None))
        {
        }

        // Assert
        resource.Verify(r => r.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task ChatAsync_WhenProviderThrowsClientResultException_ShouldReturnFailure()
    {
        // Arrange
        var mockClient = new Mock<IChatClient>();
        var mockAgentRepo = new Mock<IAgentRepository>();
        mockAgentRepo.Setup(r => r.FindDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinition?)null);
        mockClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ClientResultException("429 Too Many Requests"));

        // Act
        var result = await BuildService(mockClient, mockAgentRepo)
            .ChatAsync(new ChatRequest("Hi", []), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("AI service temporarily unavailable.");
    }

    [Fact]
    public async Task ChatAsync_WhenRequestedAgentDoesNotExist_ShouldFailInsteadOfUsingUnconstrainedModel()
    {
        // Arrange
        var mockClient = new Mock<IChatClient>();
        var mockAgentRepo = new Mock<IAgentRepository>();
        mockAgentRepo.Setup(r => r.FindByIdAsync("gone", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinition?)null);

        // Act
        var result = await BuildService(mockClient, mockAgentRepo)
            .ChatAsync(new ChatRequest("Hi", [], AgentId: "gone"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Agent not found.");
        mockClient.Verify(
            c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StreamChatAsync_WhenProviderThrowsClientResultException_ShouldThrowAiProviderException()
    {
        // Arrange
        var mockClient = new Mock<IChatClient>();
        var mockAgentRepo = new Mock<IAgentRepository>();
        mockAgentRepo.Setup(r => r.FindDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinition?)null);
        mockClient
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(FailingUpdates());
        var service = BuildService(mockClient, mockAgentRepo);

        // Act
        var act = async () =>
        {
            await foreach (var _ in service.StreamChatAsync(new ChatRequest("Hi", []), CancellationToken.None))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<AiProviderException>();
    }

    [Fact]
    public async Task StreamChatAsync_WhenRequestedAgentDoesNotExist_ShouldThrowAgentNotFoundException()
    {
        // Arrange
        var mockAgentRepo = new Mock<IAgentRepository>();
        mockAgentRepo.Setup(r => r.FindByIdAsync("gone", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinition?)null);
        var service = BuildService(agentRepo: mockAgentRepo);

        // Act
        var act = async () =>
        {
            await foreach (var _ in service.StreamChatAsync(new ChatRequest("Hi", [], AgentId: "gone"), CancellationToken.None))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<AgentNotFoundException>();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> FailingUpdates()
    {
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, "partial");
        throw new ClientResultException("429 Too Many Requests");
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> ToUpdates(params string[] texts)
    {
        foreach (var text in texts)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, text);
        }
    }
}
