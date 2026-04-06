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

        var handler = new CreateAgentHandler(mockRepo.Object);
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
    }

    [Fact]
    public async Task Handle_WhenNoToolsProvided_ShouldCreateAgentWithEmptyToolList()
    {
        // Arrange
        var mockRepo = new Mock<IAgentRepository>();
        mockRepo.Setup(r => r.AddAsync(It.IsAny<AgentDefinition>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateAgentHandler(mockRepo.Object);
        var command = new CreateAgentCommand("Agent", "Instructions.", string.Empty, false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
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
