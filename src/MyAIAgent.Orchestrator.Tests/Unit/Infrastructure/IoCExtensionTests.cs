/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Checks required service-authorization configuration at infrastructure registration boundaries.
 */

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Orchestrator.Infrastructure;
using MyAIAgent.Orchestrator.Infrastructure.Helpers;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Common.Tests;
using Microsoft.Extensions.AI;

namespace MyAIAgent.Orchestrator.Tests.Unit.Infrastructure;

public sealed class IoCExtensionTests
{
    private static IConfiguration Config(bool? serviceAuthorizationEnabled, string? mcpKey = "mcp-test-key") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:Endpoint"] = "https://test.documents.azure.com/",
                ["AzureIdentity:ClientId"] = "11111111-1111-1111-1111-111111111111",
                ["CosmosDb:DatabaseName"] = "myaiagent-prod",
                ["AiProvider:Type"] = "AzureOpenAI",
                ["AzureOpenAI:Endpoint"] = "https://example.openai.azure.com",
                ["AzureOpenAI:DeploymentName"] = "gpt-4o",
                ["AzureOpenAI:ApiKey"] = "key",
                ["ServiceAuthorization:Enabled"] = serviceAuthorizationEnabled?.ToString(),
                ["McpServer:BaseUrl"] = "https://mcp.test",
                ["McpServer:AccessKey"] = mcpKey,
                ["McpServer:AllowedExternalOrigins:0"] = "https://external.example",
            })
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void AddInfrastructure_WhenServiceAuthorizationIsOffOutsideDevelopment_ShouldFail(bool? enabled)
    {
        // Arrange & Act
        var action = () => new ServiceCollection().AddInfrastructure(Config(enabled), isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("*ServiceAuthorization*");
    }

    [Fact]
    public void AddInfrastructure_WhenServiceAuthorizationIsOnOutsideDevelopment_ShouldRegister()
    {
        // Arrange & Act
        var action = () => new ServiceCollection().AddInfrastructure(Config(true), isDevelopment: false);

        // Assert
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddInfrastructure_WhenMcpKeyIsMissingOutsideDevelopment_ShouldFail(string? key)
    {
        // Arrange & Act
        var action = () => new ServiceCollection().AddInfrastructure(Config(true, key), isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("McpServer:AccessKey*");
    }

    [Theory]
    [InlineData("https://mcp.test/mcp", "mcp-test-key")]
    [InlineData("https://mcp.test/", "mcp-test-key")]
    [InlineData("https://external.example/mcp", null)]
    public async Task GetAgentToolsAsync_WhenFactoryRegistered_ShouldAuthenticateOnlyTrustedDiscoveryAndInvocation(string endpoint, string? expectedKey)
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler(requiredKey: expectedKey);
        var services = new ServiceCollection();
        services.AddInfrastructure(Config(true), isDevelopment: false);
        services.AddHttpClient<IAgentHelper, AgentHelper>().ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddHttpClient(AgentHelper.ExternalClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();
        var helper = provider.GetRequiredService<IAgentHelper>();

        // Act
        await using var tools = await helper.GetAgentToolsAsync(new AgentDefinition
        {
            McpServerEndpoint = endpoint,
            AgentTools = [new AgentTool { Name = "test_tool", IsEnabled = true }]
        }, CancellationToken.None);
        var tool = tools.Tools.Should().ContainSingle().Which.Should().BeAssignableTo<AIFunction>().Which;
        await tool.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        // Assert
        handler.Methods.Should().ContainInOrder("initialize", "tools/list", "tools/call");
        handler.Methods.Count(method => method == "tools/call").Should().Be(1);
        handler.AuthorizationHeaders.Should().NotBeEmpty().And.OnlyContain(key => key == expectedKey);
    }
}
