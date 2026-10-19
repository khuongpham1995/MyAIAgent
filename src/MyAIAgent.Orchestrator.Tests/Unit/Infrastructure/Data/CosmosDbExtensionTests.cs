using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Orchestrator.Infrastructure.Data;

namespace MyAIAgent.Orchestrator.Tests.Unit.Infrastructure.Data;

public sealed class CosmosDbExtensionTests
{
    private static IConfiguration Config(string? endpoint = "https://test.documents.azure.com/",
        string? databaseName = "myaiagent-test", string? clientId = "11111111-1111-1111-1111-111111111111") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CosmosDb:Endpoint"] = endpoint,
            ["CosmosDb:DatabaseName"] = databaseName,
            ["AzureIdentity:ClientId"] = clientId,
        }).Build();

    [Theory]
    [InlineData("myaiagent-prod")]
    [InlineData("Orchestrator")]
    [InlineData("someone-test")]
    public void AddCosmosDb_WhenDevelopmentDatabaseIsNotTheTestDatabase_ShouldFail(string databaseName)
    {
        // Arrange & Act
        var action = () => new ServiceCollection().AddCosmosDb<OrchestratorDbContext>(Config(databaseName: databaseName), true);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("*'myaiagent-test'*");
    }

    [Theory]
    [InlineData(true, "myaiagent-test")]
    [InlineData(false, "myaiagent-prod")]
    public void AddCosmosDb_WhenEndpointAndIdentityAreConfigured_ShouldRegisterContext(bool isDevelopment, string databaseName)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCosmosDb<OrchestratorDbContext>(Config(databaseName: databaseName), isDevelopment);

        // Assert
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(OrchestratorDbContext));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://test.documents.azure.com")]
    [InlineData("https://test.documents.azure.com/?key=unsafe")]
    public void AddCosmosDb_WhenEndpointIsMissingOrInvalid_ShouldFail(string? endpoint)
    {
        // Arrange & Act
        var action = () => new ServiceCollection().AddCosmosDb<OrchestratorDbContext>(Config(endpoint: endpoint), true);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("*CosmosDb:Endpoint*");
    }

    [Fact]
    public void AddCosmosDb_WhenDeployedIdentityIsMissing_ShouldFail()
    {
        // Arrange & Act
        var action = () => new ServiceCollection().AddCosmosDb<OrchestratorDbContext>(Config(clientId: null), false);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("*AzureIdentity:ClientId*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddCosmosDb_WhenDatabaseNameIsMissing_ShouldFail(string? databaseName)
    {
        // Arrange & Act
        var action = () => new ServiceCollection().AddCosmosDb<OrchestratorDbContext>(Config(databaseName: databaseName), false);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("*DatabaseName*");
    }
}
