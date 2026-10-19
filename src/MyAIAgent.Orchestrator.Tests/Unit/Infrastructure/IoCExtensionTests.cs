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
                ["ConnectionStrings:CosmosDb"] = "AccountEndpoint=https://localhost:9/;AccountKey=eA==;",
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
        var action = () => new ServiceCollection().AddInfrastructure(Config(enabled), isDevelopment: false);

        action.Should().Throw<InvalidOperationException>().WithMessage("*ServiceAuthorization*");
    }

    [Fact]
    public void AddInfrastructure_WhenServiceAuthorizationIsOnOutsideDevelopment_ShouldRegister()
    {
        var action = () => new ServiceCollection().AddInfrastructure(Config(true), isDevelopment: false);

        action.Should().NotThrow();
    }
}
