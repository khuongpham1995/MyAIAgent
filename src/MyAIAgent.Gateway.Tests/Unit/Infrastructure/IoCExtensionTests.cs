using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Gateway.Infrastructure;

namespace MyAIAgent.Gateway.Tests.Unit.Infrastructure;

public sealed class IoCExtensionTests
{
    private const string OrchestratorKey = "orchestrator-secret-key";

    private static IConfiguration Config(string? orchestratorKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Orchestrator:BaseUrl"] = "http://orchestrator.test",
                ["KernelMemory:BaseUrl"] = "http://kernel-memory.test",
                ["McpServer:BaseUrl"] = "http://mcp.test",
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
}
