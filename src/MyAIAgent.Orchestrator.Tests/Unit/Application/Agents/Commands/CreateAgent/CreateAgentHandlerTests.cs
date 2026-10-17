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

namespace MyAIAgent.Orchestrator.Tests.Unit.Application.Agents.Commands.CreateAgent;

public sealed class CreateAgentHandlerTests
{
    [Fact]
    public async Task Handle_WhenCommandIsValid_ShouldPersistAgentAndReturnId()
    {
        // Arrange
        AgentDefinition? persisted = null;
        var mockRepo = new Mock<IAgentRepository>();
        mockRepo
            .Setup(r => r.AddAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<AgentDefinition, CancellationToken>((a, _) => persisted = a)
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateAgentHandler(mockRepo.Object, unitOfWork.Object);
        var command = new CreateAgentCommand(
            "My Agent",
            "You are helpful.",
            "http://localhost:5255",
            IsDefault: false,
            EnabledTools: ["search", "datetime"]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNullOrEmpty();
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("My Agent");
        persisted.AgentTools.Should().HaveCount(2);
        persisted.AgentTools.Should().AllSatisfy(t => t.IsEnabled.Should().BeTrue());
        unitOfWork.Verify(work => work.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoToolsProvided_ShouldCreateAgentWithEmptyToolList()
    {
        // Arrange
        var mockRepo = new Mock<IAgentRepository>();
        mockRepo.Setup(r => r.AddAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateAgentHandler(mockRepo.Object, new Mock<IUnitOfWork>().Object);
        var command = new CreateAgentCommand("Agent", "Instructions.", string.Empty, false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenRepositoryFails_ShouldNotCommit()
    {
        var repository = new Mock<IAgentRepository>();
        repository.Setup(item => item.AddAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage failed"));
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateAgentHandler(repository.Object, unitOfWork.Object);

        var action = async () => await handler.Handle(
            new CreateAgentCommand("Agent", "Instructions", string.Empty, false), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCalled_ShouldForwardCancellationTokenToRepositoryAndCommit()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new Mock<IAgentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateAgentHandler(repository.Object, unitOfWork.Object);

        await handler.Handle(
            new CreateAgentCommand("Agent", "Instructions", string.Empty, false), cancellation.Token);

        repository.Verify(item => item.AddAsync(It.IsAny<AgentDefinition>(), cancellation.Token), Times.Once);
        unitOfWork.Verify(work => work.SaveChangesAsync(cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNameIsEmpty_ShouldThrowValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateAgentHandler).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(typeof(CreateAgentHandler).Assembly, includeInternalTypes: true);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(new Mock<IAgentRepository>().Object);
        services.AddSingleton(new Mock<IChatOrchestratorService>().Object);
        services.AddSingleton(new Mock<IChatSessionRepository>().Object);
        services.AddSingleton(new Mock<IUnitOfWork>().Object);
        services.AddSingleton(TimeProvider.System);

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var act = async () => await sender.Send(
            new CreateAgentCommand("", "Instructions.", string.Empty, false), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WhenInstructionsIsEmpty_ShouldThrowValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateAgentHandler).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(typeof(CreateAgentHandler).Assembly, includeInternalTypes: true);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(new Mock<IAgentRepository>().Object);
        services.AddSingleton(new Mock<IChatOrchestratorService>().Object);
        services.AddSingleton(new Mock<IChatSessionRepository>().Object);
        services.AddSingleton(new Mock<IUnitOfWork>().Object);
        services.AddSingleton(TimeProvider.System);

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var act = async () => await sender.Send(
            new CreateAgentCommand("Agent Name", "", string.Empty, false), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
