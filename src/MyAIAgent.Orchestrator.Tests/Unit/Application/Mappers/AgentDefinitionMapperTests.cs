using FluentAssertions;
using MyAIAgent.Orchestrator.Application.Mappers;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Tests.Unit.Application.Mappers;

public sealed class AgentDefinitionMapperTests
{
    [Fact]
    public void ToSummary_WhenAgentHasEnabledTools_ShouldMapToMcpToolInfo()
    {
        // Arrange
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
        var summary = agent.ToSummary();

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
    public void ToSummary_WhenSomeToolsAreDisabled_ShouldOnlyIncludeEnabledTools()
    {
        // Arrange
        var agent = new AgentDefinition
        {
            AgentTools =
            [
                new AgentTool { Name = "web_search", IsEnabled = true },
                new AgentTool { Name = "disabled_tool", IsEnabled = false },
            ]
        };

        // Act
        var summary = agent.ToSummary();

        // Assert
        summary.EnabledTools.Should().ContainSingle();
        summary.EnabledTools[0].Name.Should().Be("web_search");
    }

    [Fact]
    public void ToSummary_WhenAllToolsAreDisabled_ShouldReturnEmptyEnabledTools()
    {
        // Arrange
        var agent = new AgentDefinition
        {
            AgentTools =
            [
                new AgentTool { Name = "tool_a", IsEnabled = false },
                new AgentTool { Name = "tool_b", IsEnabled = false },
            ]
        };

        // Act
        var summary = agent.ToSummary();

        // Assert
        summary.EnabledTools.Should().BeEmpty();
    }

    [Fact]
    public void ToSummary_WhenAgentHasNoTools_ShouldReturnEmptyEnabledTools()
    {
        // Arrange
        var agent = new AgentDefinition { AgentTools = [] };

        // Act
        var summary = agent.ToSummary();

        // Assert
        summary.EnabledTools.Should().BeEmpty();
    }
}
