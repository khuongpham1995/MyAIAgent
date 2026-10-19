using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Orchestrator.Application.Agents.Commands.CreateAgent;
using MyAIAgent.Orchestrator.Application.Chat.Commands.Chat;
using MyAIAgent.Orchestrator.Application;
using MyAIAgent.Orchestrator.Application.Agents;
using MyAIAgent.Orchestrator.Application.Chat;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Tests.Unit.Application.Common.Mappings;

public sealed class ApplicationMappingTests
{
    [Fact]
    public void Map_WhenCreateAgentHasTools_ShouldPreserveRequestFields()
    {
        // Arrange
        using var provider = CreateProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var request = new CreateAgentRequestDto("Agent", "Instructions", "https://mcp.example", true, ["web_search"]);

        // Act
        var command = mapper.Map<CreateAgentCommand>(request);

        // Assert
        command.Name.Should().Be("Agent");
        command.Instructions.Should().Be("Instructions");
        command.McpServerEndpoint.Should().Be("https://mcp.example");
        command.IsDefault.Should().BeTrue();
        command.EnabledTools.Should().Equal("web_search");
    }

    [Fact]
    public void Map_WhenChatRequestHasIdentity_ShouldPreserveRequestFields()
    {
        // Arrange
        using var provider = CreateProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var request = new ChatRequestDto("session-1", "Hello", "tenant-1:user-1", "agent-1");

        // Act
        var command = mapper.Map<ChatCommand>(request);

        // Assert
        command.SessionId.Should().Be("session-1");
        command.UserMessage.Should().Be("Hello");
        command.UserId.Should().Be("tenant-1:user-1");
        command.AgentId.Should().Be("agent-1");
    }

    [Fact]
    public void Map_WhenAgentHasEnabledTools_ShouldMapToMcpToolInfo()
    {
        // Arrange
        using var provider = CreateProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var agent = new AgentDefinition
        {
            Id = "agent-1",
            Name = "Test Agent",
            Instructions = "Be helpful.",
            McpServerEndpoint = "http://localhost:5255",
            IsDefault = true,
            AgentTools =
            [
                new AgentTool { Name = "web_search", IsEnabled = true },
                new AgentTool { Name = "date_time", IsEnabled = true },
            ]
        };

        // Act
        var summary = mapper.Map<AgentSummary>(agent);

        // Assert
        summary.Id.Should().Be("agent-1");
        summary.Name.Should().Be("Test Agent");
        summary.Instructions.Should().Be("Be helpful.");
        summary.McpServerEndpoint.Should().Be("http://localhost:5255");
        summary.IsDefault.Should().BeTrue();
        summary.EnabledTools.Should().HaveCount(2);
        summary.EnabledTools[0].Name.Should().Be("web_search");
        summary.EnabledTools[0].Description.Should().BeNull();
        summary.EnabledTools[1].Name.Should().Be("date_time");
    }

    [Fact]
    public void Map_WhenSomeToolsAreDisabled_ShouldOnlyIncludeEnabledTools()
    {
        // Arrange
        using var provider = CreateProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var agent = new AgentDefinition
        {
            AgentTools =
            [
                new AgentTool { Name = "web_search", IsEnabled = true },
                new AgentTool { Name = "disabled_tool", IsEnabled = false },
            ]
        };

        // Act
        var summary = mapper.Map<AgentSummary>(agent);

        // Assert
        summary.EnabledTools.Should().ContainSingle();
        summary.EnabledTools[0].Name.Should().Be("web_search");
    }

    [Fact]
    public void Map_WhenAllToolsAreDisabled_ShouldReturnEmptyEnabledTools()
    {
        // Arrange
        using var provider = CreateProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var agent = new AgentDefinition
        {
            AgentTools =
            [
                new AgentTool { Name = "tool_a", IsEnabled = false },
                new AgentTool { Name = "tool_b", IsEnabled = false },
            ]
        };

        // Act
        var summary = mapper.Map<AgentSummary>(agent);

        // Assert
        summary.EnabledTools.Should().BeEmpty();
    }

    [Fact]
    public void Map_WhenAgentHasNoTools_ShouldReturnEmptyEnabledTools()
    {
        // Arrange
        using var provider = CreateProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var agent = new AgentDefinition { AgentTools = [] };

        // Act
        var summary = mapper.Map<AgentSummary>(agent);

        // Assert
        summary.EnabledTools.Should().BeEmpty();
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        return services.BuildServiceProvider();
    }
}
