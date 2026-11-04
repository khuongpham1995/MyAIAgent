/**
 * MyAIAgent | KernelMemory · Tests
 *
 * Checks supported provider wiring and rejection of missing credentials or unsupported orchestration.
 */

using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.Pipeline;
using Moq;
using MyAIAgent.KernelMemory.Infrastructure.Extensions;
using MyAIAgent.KernelMemory.Infrastructure.Services;
using UglyToad.PdfPig.Writer;

namespace MyAIAgent.KernelMemory.Tests.Unit.Infrastructure;

public sealed class KernelMemoryProviderExtensionTests
{
    [Fact]
    public void EnsureSupportedMemoryDbTypes_WhenAzureAiSearch_ShouldPass()
    {
        // Arrange
        var types = new[] { "AzureAISearch", "azureaisearch" };

        // Act
        var action = () => KernelMemoryProviderExtension.EnsureSupportedMemoryDbTypes(types);

        // Assert
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("AzureCosmosDbNoSQL")]
    [InlineData("Qdrant")]
    [InlineData("Nonsense")]
    public void EnsureSupportedMemoryDbTypes_WhenTypeIsNotWiredUp_ShouldFailNamingTheType(string type)
    {
        // Arrange
        var types = new[] { type };

        // Act
        var action = () => KernelMemoryProviderExtension.EnsureSupportedMemoryDbTypes(types);

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
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();

        // Act
        var indexName = configuration["KernelMemory:DefaultIndexName"];
        var databaseName = configuration["CosmosDb:DatabaseName"];
        var ocrEnabled = configuration.GetValue<bool>("KernelMemory:Ocr:Enabled");

        // Assert
        indexName.Should().Be("myaiagent-test");
        databaseName.Should().Be("myaiagent-test");
        ocrEnabled.Should().BeFalse();
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

    [Fact]
    public void AddConfiguredProviders_WhenOcrDisabled_ShouldNotRequireAzureOcrConfiguration()
    {
        // Arrange
        var (configuration, config) = BuildConfiguration();
        var builder = new KernelMemoryBuilder();

        // Act
        var action = () => builder.AddConfiguredProviders(configuration, config);

        // Assert
        action.Should().NotThrow();
        builder.Services.Where(x => x.ServiceType == typeof(IContentDecoder))
            .Should().NotContain(x => x.ImplementationType == typeof(DocumentIntelligenceImageDecoder)
                || x.ImplementationType == typeof(DocumentIntelligencePdfDecoder));
    }

    [Theory]
    [InlineData("", "ApiKey", "key", "ClientId", "Endpoint")]
    [InlineData("http://example.com", "ApiKey", "key", "ClientId", "HTTPS")]
    [InlineData("https://example.com", "", "key", "ClientId", "Auth")]
    [InlineData("https://example.com", "ApiKey", "", "ClientId", "APIKey")]
    [InlineData("https://example.com", "ManagedIdentity", "", "", "ClientId")]
    public void AddConfiguredProviders_WhenEnabledOcrConfigurationIsIncomplete_ShouldFailAtStartup(
        string endpoint, string auth, string apiKey, string clientId, string expected)
    {
        // Arrange
        var (baseConfiguration, _) = BuildConfiguration();
        var configuration = WithOcr(baseConfiguration, endpoint, auth, apiKey, clientId);
        var config = configuration.GetSection("KernelMemory").Get<KernelMemoryConfig>()!;

        // Act
        var action = () => new KernelMemoryBuilder().AddConfiguredProviders(configuration, config);

        // Assert
        action.Should().Throw<ConfigurationException>().WithMessage($"*{expected}*");
    }

    [Theory]
    [InlineData("ApiKey", "key", "")]
    [InlineData("ManagedIdentity", "", "client-id")]
    public void AddConfiguredProviders_WhenEnabledOcrConfigurationIsValid_ShouldRegisterCustomDecodersLast(
        string auth, string apiKey, string clientId)
    {
        // Arrange
        var (baseConfiguration, _) = BuildConfiguration();
        var configuration = WithOcr(baseConfiguration,
            "https://example.cognitiveservices.azure.com", auth, apiKey, clientId);
        var config = configuration.GetSection("KernelMemory").Get<KernelMemoryConfig>()!;
        var services = new ServiceCollection();
        var builder = new KernelMemoryBuilder(services);

        services.AddLogging();

        // Act
        builder.AddConfiguredProviders(configuration, config);
        using var provider = services.BuildServiceProvider();
        var decoders = provider.GetServices<IContentDecoder>().ToList();

        // Assert
        decoders.Should().Contain(x => x.GetType().Name == "PdfDecoder");
        decoders.Should().Contain(x => x.GetType().Name == "ImageDecoder");
        decoders.Last(x => x.SupportsMimeType("application/pdf"))
            .Should().BeOfType<DocumentIntelligencePdfDecoder>();
        decoders.Last(x => x.SupportsMimeType("image/jpeg"))
            .Should().BeOfType<DocumentIntelligenceImageDecoder>();
    }

    [Fact]
    public void AddKernelMemory_WhenOcrEnabled_ShouldResolveSelectedDecodersFromApplicationServices()
    {
        // Arrange
        var (baseConfiguration, _) = BuildConfiguration();
        var configuration = WithOcr(baseConfiguration,
            "https://example.cognitiveservices.azure.com", "ApiKey", "key", "");
        var config = configuration.GetSection("KernelMemory").Get<KernelMemoryConfig>()!;
        var builder = WebApplication.CreateBuilder();

        // Act
        builder.AddKernelMemory(
            memoryBuilder => memoryBuilder.AddConfiguredProviders(configuration, config),
            buildOptions: new KernelMemoryBuilderBuildOptions { AllowMixingVolatileAndPersistentData = true });
        using var app = builder.Build();
        var decoders = app.Services.GetServices<IContentDecoder>().ToList();
        var detector = app.Services.GetRequiredService<IMimeTypeDetection>();

        // Assert
        detector.GetFileType("scan.heif").Should().Be("image/heif");
        foreach (var mimeType in new[] { "image/jpeg", "image/png", "image/tiff", "image/bmp", "image/heif" })
            decoders.Last(x => x.SupportsMimeType(mimeType)).Should().BeOfType<DocumentIntelligenceImageDecoder>();

        decoders.Last(x => x.SupportsMimeType("application/pdf"))
            .Should().BeOfType<DocumentIntelligencePdfDecoder>();
        foreach (var mimeType in new[]
                 {
                     "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                     "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                     "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                     "text/html"
                 })
            decoders.Last(x => x.SupportsMimeType(mimeType)).Should().BeOfType<DocumentIntelligenceOfficeDecoder>();

        decoders.Last(x => x.SupportsMimeType("text/plain"))
            .Should().NotBeOfType<DocumentIntelligenceOfficeDecoder>();
    }

    [Fact]
    public async Task TextExtractionHandler_WhenPdfHasNoNativeText_ShouldUseRegisteredOcrDecoder()
    {
        // Arrange
        var (baseConfiguration, _) = BuildConfiguration();
        var configuration = WithOcr(baseConfiguration,
            "https://example.cognitiveservices.azure.com", "ApiKey", "key", "");
        var config = configuration.GetSection("KernelMemory").Get<KernelMemoryConfig>()!;
        var services = new ServiceCollection();
        new KernelMemoryBuilder(services).AddConfiguredProviders(configuration, config);
        var ocr = new Mock<IDocumentIntelligenceOcrClient>();
        ocr.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OcrPage> { new(1, "OCR from blank PDF") });
        services.AddSingleton(ocr.Object);
        services.AddLogging();
        using var provider = services.BuildServiceProvider();

        using var pdfBuilder = new PdfDocumentBuilder();
        pdfBuilder.AddPage(300, 300);
        var pdf = BinaryData.FromBytes(pdfBuilder.Build());
        var pipeline = new DataPipeline { Index = "test", DocumentId = "scan" };
        pipeline.Files.Add(new DataPipeline.FileDetails
        {
            Id = "file-1",
            Name = "scan.pdf",
            MimeType = "application/pdf",
            Size = pdf.ToMemory().Length
        });
        var stored = new Dictionary<string, BinaryData>();
        var orchestrator = new Mock<IPipelineOrchestrator>();
        orchestrator.Setup(x => x.ReadFileAsync(pipeline, "scan.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdf);
        orchestrator.Setup(x => x.WriteFileAsync(pipeline, It.IsAny<string>(),
                It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .Callback<DataPipeline, string, BinaryData, CancellationToken>((_, name, content, _) =>
                stored[name] = content)
            .Returns(Task.CompletedTask);
        using var handler = new TextExtractionHandler("extract", orchestrator.Object,
            provider.GetServices<IContentDecoder>());

        // Act
        await handler.InvokeAsync(pipeline);

        // Assert
        stored["scan.pdf.extract.txt"].ToString().Should().Contain("OCR from blank PDF");
        ocr.Verify(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("scan.bmp", "image/bmp")]
    [InlineData("scan.heif", "image/heif")]
    public async Task TextExtractionHandler_WhenNewImageUploaded_ShouldDetectMimeAndExtractOcrText(
        string filename, string expectedMimeType)
    {
        // Arrange
        var (baseConfiguration, _) = BuildConfiguration();
        var configuration = WithOcr(baseConfiguration,
            "https://example.cognitiveservices.azure.com", "ApiKey", "key", "");
        var config = configuration.GetSection("KernelMemory").Get<KernelMemoryConfig>()!;
        var services = new ServiceCollection();
        new KernelMemoryBuilder(services).AddConfiguredProviders(configuration, config);
        var ocr = new Mock<IDocumentIntelligenceOcrClient>();
        ocr.Setup(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OcrPage> { new(1, "OCR image text") });
        services.AddSingleton(ocr.Object);
        services.AddLogging();
        using var provider = services.BuildServiceProvider();
        var detector = provider.GetRequiredService<Microsoft.KernelMemory.Pipeline.IMimeTypeDetection>();
        var mimeType = detector.GetFileType(filename);
        var data = BinaryData.FromBytes([1, 2, 3]);
        var pipeline = new DataPipeline { Index = "test", DocumentId = "image" };
        pipeline.Files.Add(new DataPipeline.FileDetails
        {
            Id = "file-1",
            Name = filename,
            MimeType = mimeType,
            Size = data.ToMemory().Length
        });
        var stored = new Dictionary<string, BinaryData>();
        var orchestrator = new Mock<IPipelineOrchestrator>();
        orchestrator.Setup(x => x.ReadFileAsync(pipeline, filename, It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);
        orchestrator.Setup(x => x.WriteFileAsync(pipeline, It.IsAny<string>(),
                It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()))
            .Callback<DataPipeline, string, BinaryData, CancellationToken>((_, name, content, _) =>
                stored[name] = content)
            .Returns(Task.CompletedTask);
        using var handler = new TextExtractionHandler("extract", orchestrator.Object,
            provider.GetServices<IContentDecoder>());

        // Act
        await handler.InvokeAsync(pipeline);

        // Assert
        mimeType.Should().Be(expectedMimeType);
        stored[$"{filename}.extract.txt"].ToString().Should().Contain("OCR image text");
        ocr.Verify(x => x.AnalyzeAsync(It.IsAny<BinaryData>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IConfiguration WithOcr(IConfiguration baseConfiguration,
        string endpoint, string auth, string apiKey, string clientId) =>
        new ConfigurationBuilder()
            .AddConfiguration(baseConfiguration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KernelMemory:Ocr:Enabled"] = "true",
                ["KernelMemory:Ocr:Tier"] = "F0",
                ["KernelMemory:Ocr:Endpoint"] = endpoint,
                ["KernelMemory:Ocr:Auth"] = auth,
                ["KernelMemory:Ocr:APIKey"] = apiKey,
                ["KernelMemory:Ocr:ClientId"] = clientId
            })
            .Build();
}
