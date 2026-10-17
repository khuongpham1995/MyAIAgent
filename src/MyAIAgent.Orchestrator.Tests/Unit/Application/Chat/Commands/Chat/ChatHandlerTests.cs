using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Application.Chat.Commands.Chat;
using MyAIAgent.Orchestrator.Application.Interfaces;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Domain.Models;

namespace MyAIAgent.Orchestrator.Tests.Unit.Application.Chat.Commands.Chat;

public sealed class ChatHandlerTests
{
    private static ChatHandler BuildHandler(
        Mock<IChatOrchestratorService>? orchestrator = null,
        Mock<IChatSessionRepository>? sessionRepo = null,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        return new ChatHandler(
            (orchestrator ?? new Mock<IChatOrchestratorService>()).Object,
            (sessionRepo ?? new Mock<IChatSessionRepository>()).Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object,
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
            .ReturnsAsync(ResponseModel<string>.Succeed("Hello there!"));

        mockRepo
            .Setup(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = BuildHandler(mockOrchestrator, mockRepo, unitOfWork);
        var command = new ChatCommand(null, "Hi", "tenant:user");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Reply.Should().Be("Hello there!");
        result.Data.SessionId.Should().NotBeNullOrEmpty();
        mockRepo.Verify(r => r.AddAsync(It.Is<ChatSession>(s => s.UserId == "tenant:user"), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenExistingSessionId_ShouldUpdateSessionAndReturnReply()
    {
        // Arrange
        var existingSession = new ChatSession { SessionId = "session-123", UserId = "tenant:user", Title = "Existing" };
        var mockOrchestrator = new Mock<IChatOrchestratorService>();
        var mockRepo = new Mock<IChatSessionRepository>();

        mockOrchestrator
            .Setup(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<string>.Succeed("Reply"));

        mockRepo
            .Setup(r => r.FindBySessionIdAsync("session-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSession);

        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = BuildHandler(mockOrchestrator, mockRepo, unitOfWork);
        var command = new ChatCommand("session-123", "Hello again", "tenant:user");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.SessionId.Should().Be("session-123");
        mockRepo.Verify(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(work => work.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSessionBelongsToAnotherUser_ShouldRejectBeforeOrchestration()
    {
        var existingSession = new ChatSession { SessionId = "session-123", UserId = "tenant:owner" };
        var mockOrchestrator = new Mock<IChatOrchestratorService>();
        var mockRepo = new Mock<IChatSessionRepository>();
        mockRepo
            .Setup(r => r.FindBySessionIdAsync("session-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSession);
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = BuildHandler(mockOrchestrator, mockRepo, unitOfWork);

        var result = await handler.Handle(
            new ChatCommand("session-123", "Reveal the conversation", "tenant:other"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Chat session not found.");
        mockOrchestrator.Verify(
            service => service.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrchestrationFails_ShouldReturnFailure()
    {
        // Arrange
        var mockOrchestrator = new Mock<IChatOrchestratorService>();
        mockOrchestrator
            .Setup(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<string>.Fail(message: "AI unavailable."));

        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = BuildHandler(mockOrchestrator, unitOfWork: unitOfWork);
        var command = new ChatCommand(null, "Hi", "tenant:user");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("AI unavailable.");
        unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
            .ReturnsAsync(ResponseModel<string>.Succeed("OK"));

        var handler = BuildHandler(mockOrchestrator);
        var command = new ChatCommand(null, "Ask something", "tenant:user", AgentId: "agent-42");

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
        services.AddSingleton(new Mock<IUnitOfWork>().Object);
        services.AddSingleton(TimeProvider.System);

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var act = async () => await sender.Send(new ChatCommand(null, "", "tenant:user"), CancellationToken.None);

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
        services.AddSingleton(new Mock<IUnitOfWork>().Object);
        services.AddSingleton(TimeProvider.System);

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var tooLongMessage = new string('x', 4001);

        // Act
        var act = async () => await sender.Send(new ChatCommand(null, tooLongMessage, "tenant:user"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
