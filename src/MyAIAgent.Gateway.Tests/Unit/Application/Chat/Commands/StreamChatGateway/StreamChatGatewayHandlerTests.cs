using FluentAssertions;
using Moq;
using MyAIAgent.Common.Tests;
using MyAIAgent.Gateway.Application.Chat;
using MyAIAgent.Gateway.Application.Chat.Commands.StreamChatGateway;
using MyAIAgent.Gateway.Application.Common.Interfaces;

namespace MyAIAgent.Gateway.Tests.Unit.Application.Chat.Commands.StreamChatGateway;

public sealed class StreamChatGatewayHandlerTests
{
    [Fact]
    public async Task Handle_WhenCommandIsValid_ShouldRelayEventsFromStreamClient()
    {
        // Arrange
        var client = new Mock<IOrchestratorStreamClient>();
        var expected = new[] { new ChatStreamEvent("session", SessionId: "s-1"), new ChatStreamEvent("done", SessionId: "s-1") };
        client
            .Setup(c => c.StreamChatAsync("s-1", "Hi", "visitor:a", "agent-1", It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerableHelper.ToAsyncEnumerable(expected));
        var handler = new StreamChatGatewayHandler(client.Object);

        var events = new List<ChatStreamEvent>();

        // Act
        await foreach (var chatEvent in handler.Handle(
            new StreamChatGatewayCommand("s-1", "Hi", "visitor:a", "agent-1"), CancellationToken.None))
        {
            events.Add(chatEvent);
        }

        // Assert
        events.Should().Equal(expected);
    }

    [Theory]
    [InlineData("", "visitor:a")]
    [InlineData("hi", "")]
    public async Task Validate_WhenRequiredFieldIsMissing_ShouldFail(string message, string userId)
    {
        // Arrange & Act
        var result = await new StreamChatGatewayCommandValidator()
            .ValidateAsync(new StreamChatGatewayCommand(null, message, userId));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WhenMessageExceedsPublicLimit_ShouldFail()
    {
        // Arrange
        var message = new string('x', StreamChatGatewayCommandValidator.MaxMessageLength + 1);

        // Act
        var result = await new StreamChatGatewayCommandValidator()
            .ValidateAsync(new StreamChatGatewayCommand(null, message, "visitor:a"));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("s 1")]
    public async Task Validate_WhenSessionIdHasUnsafeCharacters_ShouldFail(string sessionId)
    {
        // Arrange & Act
        var result = await new StreamChatGatewayCommandValidator()
            .ValidateAsync(new StreamChatGatewayCommand(sessionId, "hi", "visitor:a"));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WhenSessionIdIsTooLong_ShouldFail()
    {
        // Arrange & Act
        var result = await new StreamChatGatewayCommandValidator()
            .ValidateAsync(new StreamChatGatewayCommand(new string('a', 65), "hi", "visitor:a"));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WhenSessionIdIsGuid_ShouldPass()
    {
        // Arrange & Act
        var result = await new StreamChatGatewayCommandValidator()
            .ValidateAsync(new StreamChatGatewayCommand(Guid.NewGuid().ToString(), "hi", "visitor:a"));

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
