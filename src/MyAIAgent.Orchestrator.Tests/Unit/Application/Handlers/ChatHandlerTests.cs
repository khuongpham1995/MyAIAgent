using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Orchestrator.Application.Handlers;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Domain.Interfaces;
using MyAIAgent.Orchestrator.Domain.Models;

namespace MyAIAgent.Orchestrator.Tests.Unit.Application.Handlers;

public sealed class ChatHandlerTests
{
    private static ChatHandler BuildHandler(
        Mock<IChatOrchestratorService>? orchestrator = null,
        Mock<IChatSessionRepository>? sessionRepo = null)
    {
        return new ChatHandler(
            (orchestrator ?? new Mock<IChatOrchestratorService>()).Object,
            (sessionRepo ?? new Mock<IChatSessionRepository>()).Object,
            TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenNewSession_ShouldCreateSessionAndReturnReply()
    {
        // Arrange
        var mockOrchestrator = new Mock<IChatOrchestratorService>();
        var mockRepo = new Mock<IChatSessionRepository>();

        mockOrchestrator
            .Setup(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Common.Domain.Models.ResponseModel<string>.Succeed("Hello there!"));

        mockRepo
            .Setup(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(mockOrchestrator, mockRepo);
        var command = new ChatCommand(null, "Hi");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Reply.Should().Be("Hello there!");
        result.Data.SessionId.Should().NotBeNullOrEmpty();
        mockRepo.Verify(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenExistingSessionId_ShouldUpdateSessionAndReturnReply()
    {
        // Arrange
        var existingSession = new ChatSession { SessionId = "session-123", Title = "Existing" };
        var mockOrchestrator = new Mock<IChatOrchestratorService>();
        var mockRepo = new Mock<IChatSessionRepository>();

        mockOrchestrator
            .Setup(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Common.Domain.Models.ResponseModel<string>.Succeed("Reply"));

        mockRepo
            .Setup(r => r.FindBySessionIdAsync("session-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSession);

        mockRepo
            .Setup(r => r.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = BuildHandler(mockOrchestrator, mockRepo);
        var command = new ChatCommand("session-123", "Hello again");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.SessionId.Should().Be("session-123");
        mockRepo.Verify(r => r.UpdateAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()), Times.Once);
        mockRepo.Verify(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrchestrationFails_ShouldReturnFailure()
    {
        // Arrange
        var mockOrchestrator = new Mock<IChatOrchestratorService>();
        mockOrchestrator
            .Setup(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Common.Domain.Models.ResponseModel<string>.Fail(message: "AI unavailable."));

        var handler = BuildHandler(mockOrchestrator);
        var command = new ChatCommand(null, "Hi");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("AI unavailable.");
    }

    [Fact]
    public async Task Handle_WhenAgentIdProvided_ShouldPassAgentIdToOrchestratorRequest()
    {
        // Arrange
        ChatRequest? capturedRequest = null;
        var mockOrchestrator = new Mock<IChatOrchestratorService>();

        mockOrchestrator
            .Setup(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChatRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(Common.Domain.Models.ResponseModel<string>.Succeed("OK"));

        var handler = BuildHandler(mockOrchestrator);
        var command = new ChatCommand(null, "Ask something", AgentId: "agent-42");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.AgentId.Should().Be("agent-42");
    }

    [Fact]
    public async Task Handle_WhenUserMessageIsEmpty_ShouldThrowValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ChatHandler).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(typeof(ChatHandler).Assembly, includeInternalTypes: true);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(new Mock<IChatOrchestratorService>().Object);
        services.AddSingleton(new Mock<IChatSessionRepository>().Object);
        services.AddSingleton(TimeProvider.System);

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var act = async () => await sender.Send(new ChatCommand(null, ""), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WhenUserMessageExceedsMaxLength_ShouldThrowValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ChatHandler).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(typeof(ChatHandler).Assembly, includeInternalTypes: true);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(new Mock<IChatOrchestratorService>().Object);
        services.AddSingleton(new Mock<IChatSessionRepository>().Object);
        services.AddSingleton(TimeProvider.System);

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var tooLongMessage = new string('x', 4001);

        // Act
        var act = async () => await sender.Send(new ChatCommand(null, tooLongMessage), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
