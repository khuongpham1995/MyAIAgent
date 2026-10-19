using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DocumentStorage.DevTools;

namespace MyAIAgent.KernelMemory.Infrastructure.Extensions;

public static class KernelMemoryProviderExtension
{
    public const string AzureAiSearch = "AzureAISearch";
    public const string AzureBlobs = "AzureBlobs";
    public const string SimpleFileStorage = "SimpleFileStorage";

    // The memory DB types KernelMemory 0.98 can actually build; anything else fails at startup with a clear message.
    public static readonly IReadOnlyList<string> SupportedMemoryDbTypes = [AzureAiSearch];

    public static void AddConfiguredProviders(
        this IKernelMemoryBuilder memoryBuilder,
        IConfiguration configuration,
        KernelMemoryConfig config)
    {
        var services = memoryBuilder.Services;

        // Only in-process orchestration is wired; no queues or handler services are configured for distributed mode.
        if (!string.Equals(config.DataIngestion.OrchestrationType, KernelMemoryConfig.OrchestrationTypeInProcess, StringComparison.OrdinalIgnoreCase))
            throw new ConfigurationException(
                $"DataIngestion:OrchestrationType '{config.DataIngestion.OrchestrationType}' is not supported; use '{KernelMemoryConfig.OrchestrationTypeInProcess}'.");

        memoryBuilder
            .WithSearchClientConfig(config.Retrieval.SearchClient)
            .WithCustomTextPartitioningOptions(config.DataIngestion.TextPartitioning);

        var textConfig = Bind<AzureOpenAIConfig>(configuration, "AzureOpenAIText");
        var embeddingConfig = Bind<AzureOpenAIConfig>(configuration, "AzureOpenAIEmbedding");
        RequireApiKey("AzureOpenAIText", textConfig.Auth, textConfig.Endpoint, textConfig.APIKey);
        RequireApiKey("AzureOpenAIEmbedding", embeddingConfig.Auth, embeddingConfig.Endpoint, embeddingConfig.APIKey);

        services.AddAzureOpenAITextGeneration(textConfig);
        services.AddAzureOpenAIEmbeddingGeneration(embeddingConfig);

        var memoryDbTypes = config.DataIngestion.MemoryDbTypes
            .Append(config.Retrieval.MemoryDbType)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        EnsureSupportedMemoryDbTypes(memoryDbTypes);

        if (memoryDbTypes.Count > 0)
        {
            var searchConfig = Bind<AzureAISearchConfig>(configuration, AzureAiSearch);
            RequireApiKey(AzureAiSearch, searchConfig.Auth, searchConfig.Endpoint, searchConfig.APIKey);
            services.AddAzureAISearchAsMemoryDb(searchConfig);
        }

        if (string.Equals(config.DocumentStorageType, AzureBlobs, StringComparison.OrdinalIgnoreCase))
            services.AddAzureBlobsAsDocumentStorage(Bind<AzureBlobsConfig>(configuration, AzureBlobs));
        else
            services.AddSimpleFileStorageAsDocumentStorage(Bind<SimpleFileStorageConfig>(configuration, SimpleFileStorage));
    }

    public static void EnsureSupportedMemoryDbTypes(IEnumerable<string> memoryDbTypes)
    {
        foreach (var type in memoryDbTypes)
        {
            if (!SupportedMemoryDbTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                throw new ConfigurationException(
                    $"Memory DB type '{type}' is not supported. Supported types: {string.Join(", ", SupportedMemoryDbTypes)}.");
        }
    }

    // A blank secret would otherwise pass startup and only fail on the first ingest or search.
    private static void RequireApiKey(string serviceName, object auth, string endpoint, string apiKey)
    {
        if (!string.Equals(auth.ToString(), "ApiKey", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
            throw new ConfigurationException(
                $"KernelMemory:Services:{serviceName} needs both Endpoint and APIKey when Auth is ApiKey " +
                "(set them with dotnet user-secrets locally, or Key Vault in deployment).");
    }

    private static T Bind<T>(IConfiguration configuration, string serviceName)
        where T : new() =>
        configuration.GetSection($"KernelMemory:Services:{serviceName}").Get<T>()
        ?? throw new ConfigurationException($"Configuration section 'KernelMemory:Services:{serviceName}' is missing.");
}
