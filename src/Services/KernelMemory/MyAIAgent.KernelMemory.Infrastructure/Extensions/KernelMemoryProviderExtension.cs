/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Binds supported Azure providers and rejects unsupported memory databases or orchestration settings at startup.
 */

using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.DataFormats.Pdf;
using Microsoft.KernelMemory.DataFormats.Office;
using Microsoft.KernelMemory.DataFormats.WebPages;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using MyAIAgent.KernelMemory.Infrastructure.Models;
using MyAIAgent.KernelMemory.Infrastructure.Services;

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

        AddOcrWhenEnabled(memoryBuilder, configuration);
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

    private static void AddOcrWhenEnabled(IKernelMemoryBuilder memoryBuilder, IConfiguration configuration)
    {
        var services = memoryBuilder.Services;
        var options = configuration.GetSection("KernelMemory:Ocr").Get<DocumentIntelligenceOcrOptions>()
            ?? new DocumentIntelligenceOcrOptions();
        if (!options.Enabled) return;

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(endpoint.Host))
            throw new ConfigurationException("KernelMemory:Ocr:Endpoint must be an absolute HTTPS URL when OCR is enabled.");

        if (!string.Equals(options.Tier, "F0", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.Tier, "S0", StringComparison.OrdinalIgnoreCase))
            throw new ConfigurationException("KernelMemory:Ocr:Tier must be F0 or S0.");

        if (string.Equals(options.Auth, "ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey))
                throw new ConfigurationException("KernelMemory:Ocr:APIKey is required when Auth is ApiKey.");
        }
        else if (string.Equals(options.Auth, "ManagedIdentity", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.ClientId))
                throw new ConfigurationException("KernelMemory:Ocr:ClientId is required when Auth is ManagedIdentity.");
        }
        else
        {
            throw new ConfigurationException("KernelMemory:Ocr:Auth must be ApiKey or ManagedIdentity.");
        }

        var clientOptions = new DocumentIntelligenceClientOptions(
            DocumentIntelligenceClientOptions.ServiceVersion.V2024_11_30);
        var client = string.Equals(options.Auth, "ApiKey", StringComparison.OrdinalIgnoreCase)
            ? new DocumentIntelligenceClient(endpoint, new AzureKeyCredential(options.ApiKey), clientOptions)
            : new DocumentIntelligenceClient(endpoint,
                new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(options.ClientId)), clientOptions);

        // OCR is optional and KernelMemory selects the last matching decoder. Register through its
        // service pool after the defaults so both its internal and host providers see this order.
        // Marker scanning would register OCR when disabled and cannot supply per-MIME Office instances.
        memoryBuilder.WithCustomMimeTypeDetection(new HeifMimeTypeDetection());
        services.AddSingleton(options);
        services.AddSingleton(client);
        services.AddSingleton<IDocumentIntelligenceOcrClient, DocumentIntelligenceOcrClient>();
        services.AddSingleton<IContentDecoder, DocumentIntelligenceImageDecoder>();
        services.AddSingleton<IContentDecoder>(provider => new DocumentIntelligencePdfDecoder(
            provider.GetRequiredService<IDocumentIntelligenceOcrClient>(),
#pragma warning disable KMEXP00
            new PdfDecoder(provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()),
#pragma warning restore KMEXP00
            options));

#pragma warning disable KMEXP00
        services.AddSingleton<IContentDecoder>(provider => new DocumentIntelligenceOfficeDecoder(
            provider.GetRequiredService<IDocumentIntelligenceOcrClient>(), new MsWordDecoder(), options,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
        services.AddSingleton<IContentDecoder>(provider => new DocumentIntelligenceOfficeDecoder(
            provider.GetRequiredService<IDocumentIntelligenceOcrClient>(), new MsExcelDecoder(), options,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        services.AddSingleton<IContentDecoder>(provider => new DocumentIntelligenceOfficeDecoder(
            provider.GetRequiredService<IDocumentIntelligenceOcrClient>(), new MsPowerPointDecoder(), options,
            "application/vnd.openxmlformats-officedocument.presentationml.presentation"));
        services.AddSingleton<IContentDecoder>(provider => new DocumentIntelligenceOfficeDecoder(
            provider.GetRequiredService<IDocumentIntelligenceOcrClient>(), new HtmlDecoder(), options,
            "text/html"));
#pragma warning restore KMEXP00
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
