using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Orchestrator.Infrastructure;

namespace MyAIAgent.Orchestrator.Tests.Unit.Infrastructure;

public sealed class IoCExtensionTests
{
    private static IConfiguration Config(bool? serviceAuthorizationEnabled) =>
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
}
