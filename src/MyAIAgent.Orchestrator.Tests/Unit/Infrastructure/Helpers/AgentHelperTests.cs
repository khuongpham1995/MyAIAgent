/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Checks MCP tool discovery, retained connection lifetime, cancellation, and absent endpoints.
 */

using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using MyAIAgent.Common.Tests;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Infrastructure.Helpers;

namespace MyAIAgent.Orchestrator.Tests.Unit.Infrastructure.Helpers;

public sealed class AgentHelperTests
{
    [Fact]
    public async Task GetAgentToolsAsync_WhenServerUsesStreamableHttp_ShouldKeepToolsUsableUntilTurnEnds()
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var logger = new Mock<ILogger<AgentHelper>>();
        var helper = new AgentHelper(httpClient, logger.Object);
        var agent = new AgentDefinition { McpServerEndpoint = "https://mcp.example/mcp" };

        // Act
        await using var tools = await helper.GetAgentToolsAsync(agent, CancellationToken.None);

        // Assert
        var tool = tools.Tools.Should().ContainSingle("MCP discovery should succeed: {0}",
            string.Join("; ", logger.Invocations.SelectMany(call => call.Arguments))).Which.Should().BeAssignableTo<AIFunction>().Which;

        // Act
        var result = await tool.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        // Assert
        result!.ToString().Should().Contain("tool reply");
        handler.Methods.Should().ContainInOrder("initialize", "tools/list", "tools/call");
    }

    [Fact]
    public async Task GetAgentToolsAsync_WhenDiscoveryIsCancelled_ShouldPropagateCancellation()
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler(waitForCancellation: true);
        using var httpClient = new HttpClient(handler);
        var helper = new AgentHelper(httpClient, NullLogger<AgentHelper>.Instance);
        using var cancellation = new CancellationTokenSource();

        // Act
        var pending = helper.GetAgentToolsAsync(new AgentDefinition { McpServerEndpoint = "https://mcp.example/mcp" }, cancellation.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        var action = async () => await pending;

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetAgentToolsAsync_WhenEndpointIsEmpty_ShouldAvoidHttpRequests()
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var helper = new AgentHelper(httpClient, NullLogger<AgentHelper>.Instance);

        // Act
        await using var tools = await helper.GetAgentToolsAsync(new AgentDefinition(), CancellationToken.None);

        // Assert
        tools.Tools.Should().BeEmpty();
        handler.Methods.Should().BeEmpty();
    }

}
