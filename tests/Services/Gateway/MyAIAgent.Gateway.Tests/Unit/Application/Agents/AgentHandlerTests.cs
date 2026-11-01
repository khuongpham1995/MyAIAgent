/**
 * MyAIAgent | Gateway · Tests
 *
 * Covers Gateway agent creation, deletion, listing, and validation at the forwarding boundary.
 */

using MyAIAgent.Common.Application.Models;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Gateway.Application.Agents.Commands.CreateAgent;
using MyAIAgent.Gateway.Application.Agents.Commands.DeleteAgent;
using MyAIAgent.Gateway.Application.Agents.Queries.ListAgents;
using MyAIAgent.Gateway.Application.Common.Interfaces;
using MyAIAgent.Gateway.Application.Agents;

namespace MyAIAgent.Gateway.Tests.Unit.Application.Agents;

public sealed class AgentHandlerTests
{
    private static ISender BuildSender(IOrchestratorClient client)
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ListAgentsHandler).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(typeof(ListAgentsHandler).Assembly, includeInternalTypes: true);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(client);
        services.AddSingleton(new Mock<IKernelMemoryClient>().Object);
        services.AddSingleton(new Mock<IMcpServerClient>().Object);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    // --- ListAgents ---

    [Fact]
    public async Task ListAgents_WhenAgentsExist_ShouldReturnAll()
    {
        // Arrange
        IReadOnlyList<AgentSummary> agents =
        [
            new AgentSummary("id-1", "Agent One", "Instructions", "http://mcp", false, []),
            new AgentSummary("id-2", "Agent Two", "Instructions", "http://mcp", true, ["web_search"])
        ];
        var mockClient = new Mock<IOrchestratorClient>();
        mockClient
            .Setup(c => c.ListAgentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<IReadOnlyList<AgentSummary>>.Succeed(agents));

        var handler = new ListAgentsHandler(mockClient.Object);

        // Act
        var result = await handler.Handle(new ListAgentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data.Should().Contain(a => a.Name == "Agent One");
    }

    [Fact]
    public async Task ListAgents_WhenOrchestratorFails_ShouldPropagateFailure()
    {
        // Arrange
        var mockClient = new Mock<IOrchestratorClient>();
        mockClient
            .Setup(c => c.ListAgentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<IReadOnlyList<AgentSummary>>.Fail(message: "Agent listing failed."));

        var handler = new ListAgentsHandler(mockClient.Object);

        // Act
        var result = await handler.Handle(new ListAgentsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Agent listing failed.");
    }

    // --- CreateAgent ---

    [Fact]
    public async Task CreateAgent_WhenCommandIsValid_ShouldReturnAgentId()
    {
        // Arrange
        var mockClient = new Mock<IOrchestratorClient>();
        mockClient
            .Setup(c => c.CreateAgentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<string>.Succeed("new-agent-id"));

        var handler = new CreateAgentHandler(mockClient.Object);
        var command = new CreateAgentCommand("MyAgent", "You are helpful.", "http://mcp", false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("new-agent-id");
    }

    [Fact]
    public async Task CreateAgent_WhenNameIsEmpty_ShouldThrowValidationException()
    {
        // Arrange
        var sender = BuildSender(new Mock<IOrchestratorClient>().Object);

        // Act
        var act = async () => await sender.Send(
            new CreateAgentCommand("", "Instructions", "http://mcp", false),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAgent_WhenInstructionsAreEmpty_ShouldThrowValidationException()
    {
        // Arrange
        var sender = BuildSender(new Mock<IOrchestratorClient>().Object);

        // Act
        var act = async () => await sender.Send(
            new CreateAgentCommand("MyAgent", "", "http://mcp", false),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAgent_WhenMcpEndpointIsInvalidUri_ShouldThrowValidationException()
    {
        // Arrange
        var sender = BuildSender(new Mock<IOrchestratorClient>().Object);

        // Act
        var act = async () => await sender.Send(
            new CreateAgentCommand("MyAgent", "Instructions", "not-a-url", false),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    // --- DeleteAgent ---

    [Fact]
    public async Task DeleteAgent_WhenAgentExists_ShouldReturnSuccess()
    {
        // Arrange
        var mockClient = new Mock<IOrchestratorClient>();
        mockClient
            .Setup(c => c.DeleteAgentAsync("agent-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<bool>.Succeed(true));

        var handler = new DeleteAgentHandler(mockClient.Object);

        // Act
        var result = await handler.Handle(new DeleteAgentCommand("agent-id"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAgent_WhenAgentNotFound_ShouldPropagateFailure()
    {
        // Arrange
        var mockClient = new Mock<IOrchestratorClient>();
        mockClient
            .Setup(c => c.DeleteAgentAsync("missing-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<bool>.Fail(message: "Agent not found."));

        var handler = new DeleteAgentHandler(mockClient.Object);

        // Act
        var result = await handler.Handle(new DeleteAgentCommand("missing-id"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Agent not found.");
    }

    [Fact]
    public async Task DeleteAgent_WhenAgentIdIsEmpty_ShouldThrowValidationException()
    {
        // Arrange
        var sender = BuildSender(new Mock<IOrchestratorClient>().Object);

        // Act
        var act = async () => await sender.Send(new DeleteAgentCommand(""), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
