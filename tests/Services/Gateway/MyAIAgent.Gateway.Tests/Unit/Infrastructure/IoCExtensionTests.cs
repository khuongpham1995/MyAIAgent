/**
 * MyAIAgent | Gateway · Tests
 *
 * Checks required service-authorization configuration at infrastructure registration boundaries.
 */

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Gateway.Infrastructure;
using MyAIAgent.Gateway.Application.Common.Interfaces;
using MyAIAgent.Gateway.Infrastructure.Services;
using MyAIAgent.Common.Tests;

namespace MyAIAgent.Gateway.Tests.Unit.Infrastructure;

public sealed class IoCExtensionTests
{
    private const string OrchestratorKey = "orchestrator-secret-key";

    private static IConfiguration Config(string? orchestratorKey, string? mcpKey = "mcp-test-key") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Orchestrator:BaseUrl"] = "http://orchestrator.test",
                ["KernelMemory:BaseUrl"] = "http://kernel-memory.test",
                ["McpServer:BaseUrl"] = "https://mcp.test",
                ["McpServer:AccessKey"] = mcpKey,
                ["KernelMemory:AccessKey"] = "kernel-memory-secret-key",
                ["Orchestrator:AccessKey"] = orchestratorKey,
            })
            .Build();

    [Fact]
    public void AddInfrastructure_WhenOrchestratorKeyIsMissingInProduction_ShouldFail()
    {
        // Arrange & Act
        var action = () => new ServiceCollection().AddInfrastructure(Config(null), isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("*Orchestrator:AccessKey*");
    }

    [Fact]
    public void AddInfrastructure_WhenOrchestratorKeyIsMissingInDevelopment_ShouldSendNoKey()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInfrastructure(Config(null), isDevelopment: true);
        using var provider = services.BuildServiceProvider();

        // Assert
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("IOrchestratorClient");

        client.DefaultRequestHeaders.Contains("Authorization").Should().BeFalse();
    }

    [Theory]
    [InlineData("IOrchestratorClient")]
    [InlineData("IOrchestratorStreamClient")]
    public void AddInfrastructure_WhenOrchestratorKeyIsConfigured_ShouldSendItOnEveryOrchestratorClient(string clientName)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInfrastructure(Config(OrchestratorKey), isDevelopment: false);
        using var provider = services.BuildServiceProvider();

        // Assert
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);

        client.DefaultRequestHeaders.GetValues("Authorization").Should().ContainSingle().Which.Should().Be(OrchestratorKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddInfrastructure_WhenMcpKeyIsMissingOutsideDevelopment_ShouldFail(string? key)
    {
        // Arrange & Act
        var action = () => new ServiceCollection().AddInfrastructure(Config(OrchestratorKey, key), isDevelopment: false);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("McpServer:AccessKey*");
    }

    [Fact]
    public async Task ListToolsAsync_WhenClientIsFactoryRegistered_ShouldAuthenticateTheWholeProtocolSession()
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler(requiredKey: "mcp-test-key");
        var services = new ServiceCollection();
        services.AddInfrastructure(Config(OrchestratorKey), isDevelopment: false);
        services.AddHttpClient<IMcpServerClient, McpServerClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IMcpServerClient>();

        // Act
        var result = await client.ListToolsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        handler.Methods.Should().Contain("tools/list");
        handler.AuthorizationHeaders.Should().NotBeEmpty().And.OnlyContain(key => key == "mcp-test-key");
        handler.Methods.Should().NotContain("tools/call");
    }

    [Fact]
    public async Task ListToolsAsync_WhenRegisteredKeyIsRejected_ShouldReturnSafeFailureWithoutRetry()
    {
        // Arrange
        using var handler = new McpTestHttpMessageHandler(requiredKey: "different-test-key");
        var services = new ServiceCollection();
        services.AddInfrastructure(Config(OrchestratorKey), isDevelopment: false);
        services.AddHttpClient<IMcpServerClient, McpServerClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        // Act
        var result = await provider.GetRequiredService<IMcpServerClient>().ListToolsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Tool listing failed.");
        handler.AuthorizationHeaders.Should().ContainSingle();
        handler.Methods.Should().BeEmpty();
    }
}
