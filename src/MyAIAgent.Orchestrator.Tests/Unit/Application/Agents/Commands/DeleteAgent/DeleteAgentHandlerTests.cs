using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Orchestrator.Application.Agents.Commands.CreateAgent;
using MyAIAgent.Orchestrator.Application.Agents.Commands.DeleteAgent;
using MyAIAgent.Orchestrator.Application.Agents.Queries.ListAgents;
using MyAIAgent.Orchestrator.Application.Interfaces;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Tests.Unit.Application.Agents.Commands.DeleteAgent;

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
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new DeleteAgentHandler(mockRepo.Object, unitOfWork.Object);

        // Act
        var result = await handler.Handle(new DeleteAgentCommand("agent-1"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        mockRepo.Verify(r => r.Delete(agent), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAgentNotFound_ShouldReturnFailure()
    {
        // Arrange
        var mockRepo = new Mock<IAgentRepository>();
        mockRepo.Setup(r => r.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinition?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new DeleteAgentHandler(mockRepo.Object, unitOfWork.Object);

        // Act
        var result = await handler.Handle(new DeleteAgentCommand("missing-id"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Agent not found.");
        mockRepo.Verify(r => r.Delete(It.IsAny<AgentDefinition>()), Times.Never);
        unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        services.AddSingleton(new Mock<IUnitOfWork>().Object);
        services.AddSingleton(TimeProvider.System);

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var act = async () => await sender.Send(new DeleteAgentCommand(""), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
