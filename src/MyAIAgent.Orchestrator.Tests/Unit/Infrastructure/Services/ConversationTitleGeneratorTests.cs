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
        SetupResponse("\"Planning a Trip to Hanoi.\"");

        var title = await BuildGenerator().GenerateTitleAsync("Plan my trip", "Sure", CancellationToken.None);

        title.Should().Be("Planning a Trip to Hanoi");
    }

    [Fact]
    public async Task GenerateTitleAsync_WhenModelReturnsLongTitle_ShouldKeepFirstFiveWords()
    {
        SetupResponse("One two three four five six seven");

        var title = await BuildGenerator().GenerateTitleAsync("q", "a", CancellationToken.None);

        title.Should().Be("One two three four five");
    }

    [Fact]
    public async Task GenerateTitleAsync_WhenModelReturnsBlank_ShouldFallBackToUserMessage()
    {
        SetupResponse("  ");

        var title = await BuildGenerator().GenerateTitleAsync("How do I cook rice?", "a", CancellationToken.None);

        title.Should().Be("How do I cook rice?");
    }

    [Fact]
    public async Task GenerateTitleAsync_WhenProviderFails_ShouldFallBackToTruncatedUserMessage()
    {
        chatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        var title = await BuildGenerator().GenerateTitleAsync(new string('x', 300), "a", CancellationToken.None);

        title.Should().HaveLength(100);
    }

    [Fact]
    public async Task GenerateTitleAsync_WhenProviderThrowsClientResultException_ShouldFallBackToUserMessage()
    {
        chatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ClientResultException("429 Too Many Requests"));

        var title = await BuildGenerator().GenerateTitleAsync("Plan a trip", "a", CancellationToken.None);

        title.Should().Be("Plan a trip");
    }
}
