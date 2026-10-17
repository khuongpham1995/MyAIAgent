using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.MainSubSys.Application.Agents.Commands.CreateAgent;
using MyAIAgent.MainSubSys.Application.Chat.Commands.ChatGateway;
using MyAIAgent.MainSubSys.Application.Extensions;
using MyAIAgent.MainSubSys.Application.Models;

namespace MyAIAgent.MainSubSys.Tests.Unit.Application.Common.Mappings;

public sealed class ApplicationMappingTests
{
    [Fact]
    public void Map_WhenChatRequestHasAuthenticatedIdentity_ShouldUseTrustedUserId()
    {
        using var provider = CreateProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var request = new AuthenticatedChatGatewayRequest(
            new ChatGatewayRequestDto("session-1", "Hello", "agent-1"),
            "tenant-1:user-1");

        var command = mapper.Map<ChatGatewayCommand>(request);

        command.SessionId.Should().Be("session-1");
        command.UserMessage.Should().Be("Hello");
        command.UserId.Should().Be("tenant-1:user-1");
        command.AgentId.Should().Be("agent-1");
    }

    [Fact]
    public void Map_WhenCreateAgentHasTools_ShouldPreserveRequestFields()
    {
        using var provider = CreateProvider();
        var mapper = provider.GetRequiredService<IMapper>();
        var request = new CreateAgentRequestDto("Agent", "Instructions", "https://mcp.example", true, ["web_search"]);

        var command = mapper.Map<CreateAgentCommand>(request);

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
