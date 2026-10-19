using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Orchestrator.Infrastructure.Data;

namespace MyAIAgent.Orchestrator.Tests.Unit.Infrastructure.Data;

public sealed class CosmosDbExtensionTests
{
    private const string FakeConnectionString = "AccountEndpoint=https://localhost:9/;AccountKey=eA==;";

    private static IConfiguration Config(string? connectionString, string? databaseName) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CosmosDb"] = connectionString,
                ["CosmosDb:DatabaseName"] = databaseName,
            })
            .Build();

    [Theory]
    [InlineData("myaiagent-prod")]
    [InlineData("Orchestrator")]
    [InlineData("someone-test")]
    public void AddCosmosDb_WhenDevelopmentDatabaseIsNotTheTestDatabase_ShouldFail(string databaseName)
    {
        var services = new ServiceCollection();

        var action = () => services.AddCosmosDb<OrchestratorDbContext>(
            Config(FakeConnectionString, databaseName), isDevelopment: true);

        action.Should().Throw<InvalidOperationException>().WithMessage("*'myaiagent-test'*");
    }

    [Fact]
    public void AddCosmosDb_WhenDevelopmentDatabaseIsATestDatabase_ShouldRegisterContext()
    {
        var services = new ServiceCollection();

        services.AddCosmosDb<OrchestratorDbContext>(Config(FakeConnectionString, "myaiagent-test"), isDevelopment: true);

        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(OrchestratorDbContext));
    }

    [Fact]
    public void AddCosmosDb_WhenDeployedWithProductionDatabase_ShouldRegisterContext()
    {
        var services = new ServiceCollection();

        services.AddCosmosDb<OrchestratorDbContext>(Config(FakeConnectionString, "myaiagent-prod"), isDevelopment: false);

        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(OrchestratorDbContext));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddCosmosDb_WhenConnectionStringIsMissing_ShouldFailWithUserSecretsHint(string? connectionString)
    {
        var services = new ServiceCollection();

        var action = () => services.AddCosmosDb<OrchestratorDbContext>(
            Config(connectionString, "myaiagent-test"), isDevelopment: true);

        action.Should().Throw<InvalidOperationException>().WithMessage("*user-secrets*");
    }

    [Fact]
    public void AddCosmosDb_WhenDatabaseNameIsMissing_ShouldFail()
    {
        var services = new ServiceCollection();

        var action = () => services.AddCosmosDb<OrchestratorDbContext>(
            Config(FakeConnectionString, null), isDevelopment: false);

        action.Should().Throw<InvalidOperationException>().WithMessage("*DatabaseName*");
    }
}
