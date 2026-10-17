using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Chat.Commands.ChatGateway;
using MyAIAgent.MainSubSys.Application.Common.Interfaces;
using MyAIAgent.MainSubSys.Application.Chat;

namespace MyAIAgent.MainSubSys.Tests.Unit.Application.Chat.Commands.ChatGateway;

public sealed class ChatGatewayHandlerTests
{
    [Fact]
    public async Task Handle_WhenCommandIsValid_ShouldReturnSuccess()
    {
        // Arrange
        var mockClient = new Mock<IOrchestratorClient>();
        mockClient
            .Setup(c => c.ChatAsync(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<ChatGatewayResponse>.Succeed(
                new ChatGatewayResponse("Hello!", "session-1")));

        var handler = new ChatGatewayHandler(mockClient.Object);
        var command = new ChatGatewayCommand(null, "Hello", "tenant:user");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Reply.Should().Be("Hello!");
        result.Data.SessionId.Should().Be("session-1");
    }

    [Fact]
    public async Task Handle_WhenOrchestratorReturnsFail_ShouldPropagateFailure()
    {
        // Arrange
        var mockClient = new Mock<IOrchestratorClient>();
        mockClient
            .Setup(c => c.ChatAsync(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<ChatGatewayResponse>.Fail(message: "Chat request failed."));

        var handler = new ChatGatewayHandler(mockClient.Object);
        var command = new ChatGatewayCommand(null, "Hello", "tenant:user");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Chat request failed.");
    }

    [Fact]
    public async Task Handle_WhenUserMessageIsEmpty_ShouldThrowValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ChatGatewayHandler).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(typeof(ChatGatewayHandler).Assembly, includeInternalTypes: true);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(new Mock<IOrchestratorClient>().Object);

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var act = async () => await sender.Send(new ChatGatewayCommand(null, "", "tenant:user"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WhenSessionIdIsProvided_ShouldPassItToClient()
    {
        // Arrange
        var mockClient = new Mock<IOrchestratorClient>();
        mockClient
            .Setup(c => c.ChatAsync(
                "existing-session",
                "Follow up",
                "tenant:user",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<ChatGatewayResponse>.Succeed(
                new ChatGatewayResponse("Sure!", "existing-session")));

        var handler = new ChatGatewayHandler(mockClient.Object);

        // Act
        var result = await handler.Handle(
            new ChatGatewayCommand("existing-session", "Follow up", "tenant:user"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        mockClient.Verify(c => c.ChatAsync("existing-session", "Follow up", "tenant:user", null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
