/**
 * MyAIAgent | KernelMemory · Tests
 *
 * Checks supported provider wiring and rejection of missing credentials or unsupported orchestration.
 */

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using MyAIAgent.KernelMemory.Infrastructure.Extensions;

namespace MyAIAgent.KernelMemory.Tests.Unit.Infrastructure;

public sealed class KernelMemoryProviderExtensionTests
{
    [Fact]
    public void EnsureSupportedMemoryDbTypes_WhenAzureAiSearch_ShouldPass()
    {
        // Arrange & Act
        var action = () => KernelMemoryProviderExtension.EnsureSupportedMemoryDbTypes(["AzureAISearch", "azureaisearch"]);

        // Assert
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("AzureCosmosDbNoSQL")]
    [InlineData("Qdrant")]
    [InlineData("Nonsense")]
    public void EnsureSupportedMemoryDbTypes_WhenTypeIsNotWiredUp_ShouldFailNamingTheType(string type)
    {
        // Arrange & Act
        var action = () => KernelMemoryProviderExtension.EnsureSupportedMemoryDbTypes([type]);

        // Assert
        action.Should().Throw<ConfigurationException>().WithMessage($"*{type}*");
    }

    [Fact]
    public void ShippedAppSettings_WhenRead_ShouldOnlyConfigureSupportedMemoryDbTypes()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();
        var config = configuration.GetSection("KernelMemory").Get<KernelMemoryConfig>()!;

        var types = config.DataIngestion.MemoryDbTypes.Append(config.Retrieval.MemoryDbType);

        // Act
        var action = () => KernelMemoryProviderExtension.EnsureSupportedMemoryDbTypes(types);

        // Assert
        action.Should().NotThrow();
        config.DataIngestion.MemoryDbTypes.Should().Equal("AzureAISearch");
    }

    [Fact]
    public void ShippedAppSettings_WhenRead_ShouldUseTheTestIndexAndDatabaseByDefault()
    {
        // Arrange & Act
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();

        // Assert
        configuration["KernelMemory:DefaultIndexName"].Should().Be("myaiagent-test");
        configuration["CosmosDb:DatabaseName"].Should().Be("myaiagent-test");
    }

    private static (IConfiguration Configuration, KernelMemoryConfig Config) BuildConfiguration(
        string searchApiKey = "search-key",
        string openAiApiKey = "openai-key",
        string orchestration = "InProcess")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KernelMemory:DataIngestion:OrchestrationType"] = orchestration,
                ["KernelMemory:DataIngestion:MemoryDbTypes:0"] = "AzureAISearch",
                ["KernelMemory:Retrieval:MemoryDbType"] = "AzureAISearch",
                ["KernelMemory:Services:AzureOpenAIText:Auth"] = "ApiKey",
                ["KernelMemory:Services:AzureOpenAIText:Endpoint"] = "https://example.openai.azure.com",
                ["KernelMemory:Services:AzureOpenAIText:Deployment"] = "gpt-4o-mini",
                ["KernelMemory:Services:AzureOpenAIText:APIKey"] = openAiApiKey,
                ["KernelMemory:Services:AzureOpenAIEmbedding:APIType"] = "EmbeddingGeneration",
                ["KernelMemory:Services:AzureOpenAIEmbedding:Auth"] = "ApiKey",
                ["KernelMemory:Services:AzureOpenAIEmbedding:Endpoint"] = "https://example.openai.azure.com",
                ["KernelMemory:Services:AzureOpenAIEmbedding:Deployment"] = "text-embedding-ada-002",
                ["KernelMemory:Services:AzureOpenAIEmbedding:APIKey"] = openAiApiKey,
                ["KernelMemory:Services:AzureAISearch:Auth"] = "ApiKey",
                ["KernelMemory:Services:AzureAISearch:Endpoint"] = "https://example.search.windows.net",
                ["KernelMemory:Services:AzureAISearch:APIKey"] = searchApiKey,
                ["KernelMemory:Services:SimpleFileStorage:StorageType"] = "Volatile",
            })
            .Build();

        return (configuration, configuration.GetSection("KernelMemory").Get<KernelMemoryConfig>()!);
    }

    [Fact]
    public void AddConfiguredProviders_WhenConfigurationIsComplete_ShouldNotThrow()
    {
        // Arrange
        var (configuration, config) = BuildConfiguration();

        // Act
        var action = () => new KernelMemoryBuilder().AddConfiguredProviders(configuration, config);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void AddConfiguredProviders_WhenSearchApiKeyIsBlank_ShouldFailNamingTheService()
    {
        // Arrange
        var (configuration, config) = BuildConfiguration(searchApiKey: "");

        // Act
        var action = () => new KernelMemoryBuilder().AddConfiguredProviders(configuration, config);

        // Assert
        action.Should().Throw<ConfigurationException>().WithMessage("*AzureAISearch*APIKey*");
    }

    [Fact]
    public void AddConfiguredProviders_WhenOpenAiApiKeyIsBlank_ShouldFail()
    {
        // Arrange
        var (configuration, config) = BuildConfiguration(openAiApiKey: "");

        // Act
        var action = () => new KernelMemoryBuilder().AddConfiguredProviders(configuration, config);

        // Assert
        action.Should().Throw<ConfigurationException>().WithMessage("*AzureOpenAI*APIKey*");
    }

    [Fact]
    public void AddConfiguredProviders_WhenOrchestrationIsDistributed_ShouldFailWithClearMessage()
    {
        // Arrange
        var (configuration, config) = BuildConfiguration(orchestration: "Distributed");

        // Act
        var action = () => new KernelMemoryBuilder().AddConfiguredProviders(configuration, config);

        // Assert
        action.Should().Throw<ConfigurationException>().WithMessage("*InProcess*");
    }
}
