/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Verifies agent identifier generation and storage of configuration and tool entries.
 */

using FluentAssertions;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Tests.Unit.Domain.Entities;

public sealed class AgentDefinitionTests
{
    [Fact]
    public void Constructor_WhenCreated_ShouldHaveNonEmptyId()
    {
        // Arrange & Act
        var agent = new AgentDefinition();

        // Assert
        agent.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenCreated_ShouldHaveUniqueIds()
    {
        // Arrange & Act
        var first = new AgentDefinition();
        var second = new AgentDefinition();

        // Assert
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Constructor_WhenInitialized_ShouldSetPropertiesCorrectly()
    {
        // Arrange & Act
        var agent = new AgentDefinition
        {
            Name = "Test Agent",
            Instructions = "You are a helpful assistant.",
            McpServerEndpoint = "http://localhost:5255",
            IsDefault = true
        };

        // Assert
        agent.Name.Should().Be("Test Agent");
        agent.Instructions.Should().Be("You are a helpful assistant.");
        agent.McpServerEndpoint.Should().Be("http://localhost:5255");
        agent.IsDefault.Should().BeTrue();
        agent.AgentTools.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WhenToolsAdded_ShouldContainTools()
    {
        // Arrange & Act
        var agent = new AgentDefinition
        {
            AgentTools =
            [
                new AgentTool { Name = "search", IsEnabled = true },
                new AgentTool { Name = "datetime", IsEnabled = false }
            ]
        };

        // Assert
        agent.AgentTools.Should().HaveCount(2);
        agent.AgentTools.Should().ContainSingle(t => t.Name == "search" && t.IsEnabled);
        agent.AgentTools.Should().ContainSingle(t => t.Name == "datetime" && !t.IsEnabled);
    }
}
