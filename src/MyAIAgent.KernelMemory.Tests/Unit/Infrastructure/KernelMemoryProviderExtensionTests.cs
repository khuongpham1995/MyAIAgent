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
        var action = () => KernelMemoryProviderExtension.EnsureSupportedMemoryDbTypes(["AzureAISearch", "azureaisearch"]);

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("AzureCosmosDbNoSQL")]
    [InlineData("Qdrant")]
    [InlineData("Nonsense")]
    public void EnsureSupportedMemoryDbTypes_WhenTypeIsNotWiredUp_ShouldFailNamingTheType(string type)
    {
        var action = () => KernelMemoryProviderExtension.EnsureSupportedMemoryDbTypes([type]);

        action.Should().Throw<ConfigurationException>().WithMessage($"*{type}*");
    }

    [Fact]
    public void ShippedAppSettings_WhenRead_ShouldOnlyConfigureSupportedMemoryDbTypes()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();
        var config = configuration.GetSection("KernelMemory").Get<KernelMemoryConfig>()!;

        var types = config.DataIngestion.MemoryDbTypes.Append(config.Retrieval.MemoryDbType);

        var action = () => KernelMemoryProviderExtension.EnsureSupportedMemoryDbTypes(types);
        action.Should().NotThrow();
        config.DataIngestion.MemoryDbTypes.Should().Equal("AzureAISearch");
    }

    [Fact]
    public void ShippedAppSettings_WhenRead_ShouldUseTheTestIndexAndDatabaseByDefault()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();

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
        var (configuration, config) = BuildConfiguration();

        var action = () => new KernelMemoryBuilder().AddConfiguredProviders(configuration, config);

        action.Should().NotThrow();
    }

    [Fact]
    public void AddConfiguredProviders_WhenSearchApiKeyIsBlank_ShouldFailNamingTheService()
    {
        var (configuration, config) = BuildConfiguration(searchApiKey: "");

        var action = () => new KernelMemoryBuilder().AddConfiguredProviders(configuration, config);

        action.Should().Throw<ConfigurationException>().WithMessage("*AzureAISearch*APIKey*");
    }

    [Fact]
    public void AddConfiguredProviders_WhenOpenAiApiKeyIsBlank_ShouldFail()
    {
        var (configuration, config) = BuildConfiguration(openAiApiKey: "");

        var action = () => new KernelMemoryBuilder().AddConfiguredProviders(configuration, config);

        action.Should().Throw<ConfigurationException>().WithMessage("*AzureOpenAI*APIKey*");
    }

    [Fact]
    public void AddConfiguredProviders_WhenOrchestrationIsDistributed_ShouldFailWithClearMessage()
    {
        var (configuration, config) = BuildConfiguration(orchestration: "Distributed");

        var action = () => new KernelMemoryBuilder().AddConfiguredProviders(configuration, config);

        action.Should().Throw<ConfigurationException>().WithMessage("*InProcess*");
    }
}
