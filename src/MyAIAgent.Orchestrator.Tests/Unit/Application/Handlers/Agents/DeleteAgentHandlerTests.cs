using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Orchestrator.Application.Handlers.Agents;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Domain.Interfaces;

namespace MyAIAgent.Orchestrator.Tests.Unit.Application.Handlers.Agents;

public sealed class DeleteAgentHandlerTests
{
    [Fact]
    public async Task Handle_WhenAgentExists_ShouldDeleteAndReturnSuccess()
    {
        // Arrange
        var agent = new AgentDefinition { Id = "agent-1" };
        var mockRepo = new Mock<IAgentRepository>();
        mockRepo.Setup(r => r.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        mockRepo.Setup(r => r.DeleteAsync(agent, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new DeleteAgentHandler(mockRepo.Object);

        // Act
        var result = await handler.Handle(new DeleteAgentCommand("agent-1"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        mockRepo.Verify(r => r.DeleteAsync(agent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAgentNotFound_ShouldReturnFailure()
    {
        // Arrange
        var mockRepo = new Mock<IAgentRepository>();
        mockRepo.Setup(r => r.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinition?)null);

        var handler = new DeleteAgentHandler(mockRepo.Object);

        // Act
        var result = await handler.Handle(new DeleteAgentCommand("missing-id"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Agent not found.");
        mockRepo.Verify(r => r.DeleteAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAgentIdIsEmpty_ShouldThrowValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DeleteAgentHandler).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(typeof(DeleteAgentHandler).Assembly, includeInternalTypes: true);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(new Mock<IAgentRepository>().Object);
        services.AddSingleton(new Mock<IChatOrchestratorService>().Object);
        services.AddSingleton(new Mock<IChatSessionRepository>().Object);
        services.AddSingleton(TimeProvider.System);

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var act = async () => await sender.Send(new DeleteAgentCommand(""), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
