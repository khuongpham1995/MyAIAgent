/**
 * MyAIAgent | Gateway · Tests
 *
 * Verifies application mappings preserve request fields and trusted identity or enabled-tool projections.
 */

using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Gateway.Application.Agents.Commands.CreateAgent;
using MyAIAgent.Gateway.Application.Chat.Commands.ChatGateway;
using MyAIAgent.Gateway.Application;
using MyAIAgent.Gateway.Application.Agents;
using MyAIAgent.Gateway.Application.Chat;

namespace MyAIAgent.Gateway.Tests.Unit.Application.Common.Mappings;

public sealed class ApplicationMappingTests
{
    [Fact]
    public void Map_WhenChatRequestHasAuthenticatedIdentity_ShouldUseTrustedUserId()
    {
        // Arrange
        using var provider = CreateProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var request = new AuthenticatedChatGatewayRequest(
            new ChatGatewayRequestDto("session-1", "Hello", "agent-1"),
            "tenant-1:user-1");

        // Act
        var command = mapper.Map<ChatGatewayCommand>(request);

        // Assert
        command.SessionId.Should().Be("session-1");
        command.UserMessage.Should().Be("Hello");
        command.UserId.Should().Be("tenant-1:user-1");
        command.AgentId.Should().Be("agent-1");
    }

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

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        return services.BuildServiceProvider();
    }
}
