/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Checks title cleanup, length limits, and fallback when model output is blank or fails.
 */

using System.ClientModel;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Orchestrator.Infrastructure.Services;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatResponse = Microsoft.Extensions.AI.ChatResponse;

namespace MyAIAgent.Orchestrator.Tests.Unit.Infrastructure.Services;

public sealed class ConversationTitleGeneratorTests
{
    private readonly Mock<IChatClient> chatClient = new();

    private ConversationTitleGenerator BuildGenerator() =>
        new(chatClient.Object, NullLogger<ConversationTitleGenerator>.Instance);

    private void SetupResponse(string text) =>
        chatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    [Fact]
    public async Task GenerateTitleAsync_WhenModelAddsQuotesAndPunctuation_ShouldStripThem()
    {
        // Arrange
        SetupResponse("\"Planning a Trip to Hanoi.\"");

        // Act
        var title = await BuildGenerator().GenerateTitleAsync("Plan my trip", "Sure", CancellationToken.None);

        // Assert
        title.Should().Be("Planning a Trip to Hanoi");
    }

    [Fact]
    public async Task GenerateTitleAsync_WhenModelReturnsLongTitle_ShouldKeepFirstFiveWords()
    {
        // Arrange
        SetupResponse("One two three four five six seven");

        // Act
        var title = await BuildGenerator().GenerateTitleAsync("q", "a", CancellationToken.None);

        // Assert
        title.Should().Be("One two three four five");
    }

    [Fact]
    public async Task GenerateTitleAsync_WhenModelReturnsBlank_ShouldFallBackToUserMessage()
    {
        // Arrange
        SetupResponse("  ");

        // Act
        var title = await BuildGenerator().GenerateTitleAsync("How do I cook rice?", "a", CancellationToken.None);

        // Assert
        title.Should().Be("How do I cook rice?");
    }

    [Fact]
    public async Task GenerateTitleAsync_WhenProviderFails_ShouldFallBackToTruncatedUserMessage()
    {
        // Arrange
        chatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        // Act
        var title = await BuildGenerator().GenerateTitleAsync(new string('x', 300), "a", CancellationToken.None);

        // Assert
        title.Should().HaveLength(100);
    }

    [Fact]
    public async Task GenerateTitleAsync_WhenProviderThrowsClientResultException_ShouldFallBackToUserMessage()
    {
        // Arrange
        chatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ClientResultException("429 Too Many Requests"));

        // Act
        var title = await BuildGenerator().GenerateTitleAsync("Plan a trip", "a", CancellationToken.None);

        // Assert
        title.Should().Be("Plan a trip");
    }
}
