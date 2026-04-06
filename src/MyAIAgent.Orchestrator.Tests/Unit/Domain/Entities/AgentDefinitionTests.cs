using FluentAssertions;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Tests.Unit.Domain.Entities;

public sealed class AgentDefinitionTests
{
    [Fact]
    public void Constructor_WhenCreated_ShouldHaveNonEmptyId()
    {
        var agent = new AgentDefinition();

        agent.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenCreated_ShouldHaveUniqueIds()
    {
        var first = new AgentDefinition();
        var second = new AgentDefinition();

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Constructor_WhenInitialized_ShouldSetPropertiesCorrectly()
    {
        var agent = new AgentDefinition
        {
            Name = "Test Agent",
            Instructions = "You are a helpful assistant.",
            McpServerEndpoint = "http://localhost:5255",
            IsDefault = true
        };

        agent.Name.Should().Be("Test Agent");
        agent.Instructions.Should().Be("You are a helpful assistant.");
        agent.McpServerEndpoint.Should().Be("http://localhost:5255");
        agent.IsDefault.Should().BeTrue();
        agent.AgentTools.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WhenToolsAdded_ShouldContainTools()
    {
        var agent = new AgentDefinition
        {
            AgentTools =
            [
                new AgentTool { Name = "search", IsEnabled = true },
                new AgentTool { Name = "datetime", IsEnabled = false }
            ]
        };

        agent.AgentTools.Should().HaveCount(2);
        agent.AgentTools.Should().ContainSingle(t => t.Name == "search" && t.IsEnabled);
        agent.AgentTools.Should().ContainSingle(t => t.Name == "datetime" && !t.IsEnabled);
    }
}
