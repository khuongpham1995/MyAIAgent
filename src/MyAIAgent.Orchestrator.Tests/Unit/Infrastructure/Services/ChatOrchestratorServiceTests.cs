using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatResponse = Microsoft.Extensions.AI.ChatResponse;
using Moq;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Domain.Interfaces;
using MyAIAgent.Orchestrator.Domain.Models;
using MyAIAgent.Orchestrator.Infrastructure.Helpers;
using MyAIAgent.Orchestrator.Infrastructure.Services;

namespace MyAIAgent.Orchestrator.Tests.Unit.Infrastructure.Services;

public sealed class ChatOrchestratorServiceTests
{
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
            .ReturnsAsync(fakeTools);

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
            .ReturnsAsync([]);

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
}
