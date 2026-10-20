/**
 * MyAIAgent | KernelMemory · Presentation
 *
 * Maps KernelMemory daemon endpoints and health routes, then logs nonsecret startup diagnostics.
 */

using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.DocumentStorage;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Service.AspNetCore;
using MyAIAgent.KernelMemory.Presentation.Filters;

namespace MyAIAgent.KernelMemory.Presentation.Extensions;

internal static class WebApplicationExtension
{
    internal static async Task RunKernelMemoryAsync(
        this WebApplication app,
        KernelMemoryConfig config,
        DateTimeOffset start,
        string memoryType,
        int syncHandlersCount,
        int asyncHandlersCount)
    {
        MapEndpoints(app, config, start);
        LogStartupDiagnostics(app.Logger, app.Services, config, memoryType, syncHandlersCount, asyncHandlersCount);

        try
        {
            await app.RunAsync();
        }
        catch (IOException e)
        {
            app.Logger.LogError("I/O error: {ErrorMessage}", e.Message);
            Environment.Exit(-1);
        }
    }

    private static void MapEndpoints(WebApplication app, KernelMemoryConfig config, DateTimeOffset start)
    {
        if (!config.Service.RunWebService) return;

        app.MapGet("/", () => Results.Ok(
            "Kernel Memory service is running. " +
            $"Uptime: {DateTimeOffset.UtcNow.ToUnixTimeSeconds() - start.ToUnixTimeSeconds()} secs " +
            $"- Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}"))
            .WithName("Status Checker");

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var errorFilter = new HttpErrorsEndpointFilter(loggerFactory.CreateLogger<HttpErrorsEndpointFilter>());
        var authFilter = new HttpAuthEndpointFilter(config.ServiceAuthorization, loggerFactory.CreateLogger<HttpAuthEndpointFilter>());
        app.AddKernelMemoryEndpoints("/", config, [errorFilter, authFilter]);

        app.MapGet("/health", () => Results.Ok("Service is healthy."))
            .WithName("Health Checker");
        app.MapGet("/health/live", () => Results.Ok());
        app.MapGet("/health/ready", () => Results.Ok());
    }

    private static void LogStartupDiagnostics(
        ILogger logger,
        IServiceProvider services,
        KernelMemoryConfig config,
        string memoryType,
        int syncHandlersCount,
        int asyncHandlersCount)
    {
        // Kestrel default is 30 MB — used when no explicit limit is configured
        const double kestrelDefaultMaxUploadMb = 30_000_000d / 1024 / 1024;

        var environment = services.GetRequiredService<IHostEnvironment>().EnvironmentName;
        var maxUploadMb = config.Service.MaxUploadSizeMb ?? kestrelDefaultMaxUploadMb;
        var memoryDb = services.GetService<IMemoryDb>()?.GetType().FullName;
        var documentStorage = services.GetService<IDocumentStorage>()?.GetType().FullName;
        var embeddingGenerator = services.GetService<ITextEmbeddingGenerator>()?.GetType().FullName;
        var textGenerator = services.GetService<ITextGenerator>()?.GetType().FullName;

        if (config.ServiceAuthorization.Enabled
            && config.ServiceAuthorization.AccessKey1 == config.ServiceAuthorization.AccessKey2)
        {
            logger.LogError("Access keys 1 and 2 have the same value. Keys should be different to allow rotation.");
        }

        logger.LogInformation(
            "KernelMemory configured for {Environment}: {MemoryType}, {SyncHandlerCount} synchronous and {AsyncHandlerCount} asynchronous handlers, HTTP enabled: {HttpEnabled}",
            environment, memoryType, syncHandlersCount, asyncHandlersCount, config.Service.RunWebService);

        if (config.Service.RunWebService)
        {
            logger.LogInformation(
                "KernelMemory HTTP configuration: authorization enabled: {AuthorizationEnabled}, upload limit {MaxUploadMb:0.#} MiB",
                config.ServiceAuthorization.Enabled, maxUploadMb);
        }

        logger.LogInformation(
            "KernelMemory providers: memory database {MemoryDb}, document storage {DocumentStorage}, embeddings {EmbeddingGenerator}, text generation {TextGenerator}",
            memoryDb, documentStorage, embeddingGenerator, textGenerator);
    }
}
