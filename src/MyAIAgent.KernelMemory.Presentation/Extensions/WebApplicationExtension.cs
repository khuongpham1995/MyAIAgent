using System.Globalization;
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

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var envLabel = string.IsNullOrEmpty(env) ? "WARNING: ASPNETCORE_ENVIRONMENT not defined" : env;
        var handlers = $"{syncHandlersCount} synchronous / {asyncHandlersCount} asynchronous";
        var webServiceStatus = config.Service.RunWebService ? "Enabled" : "Disabled";
        var authStatus = config.ServiceAuthorization.Enabled ? "Enabled" : "Disabled";
        var maxUploadMb = (config.Service.MaxUploadSizeMb ?? kestrelDefaultMaxUploadMb).ToString("0.#", CultureInfo.CurrentCulture);
        var memoryDb = services.GetService<IMemoryDb>()?.GetType().FullName;
        var documentStorage = services.GetService<IDocumentStorage>()?.GetType().FullName;
        var embeddingGenerator = services.GetService<ITextEmbeddingGenerator>()?.GetType().FullName;
        var textGenerator = services.GetService<ITextGenerator>()?.GetType().FullName;

        if (config.ServiceAuthorization.Enabled
            && config.ServiceAuthorization.AccessKey1 == config.ServiceAuthorization.AccessKey2)
        {
            logger.LogError("Access keys 1 and 2 have the same value. Keys should be different to allow rotation.");
        }

        logger.LogInformation("*******************************************************************************");
        logger.LogInformation("* Environment         : {Environment}", envLabel);
        logger.LogInformation("* Memory type         : {MemoryType}", memoryType);
        logger.LogInformation("* Pipeline handlers   : {Handlers}", handlers);
        logger.LogInformation("* Web service         : {WebServiceStatus}", webServiceStatus);

        if (config.Service.RunWebService)
        {
            logger.LogInformation("* Web service auth    : {AuthStatus}", authStatus);
            logger.LogInformation("* Max HTTP req size   : {MaxUploadMb} Mb", maxUploadMb);
        }

        logger.LogInformation("* Memory Db           : {MemoryDb}", memoryDb);
        logger.LogInformation("* Document storage    : {DocumentStorage}", documentStorage);
        logger.LogInformation("* Embedding generation: {EmbeddingGenerator}", embeddingGenerator);
        logger.LogInformation("* Text generation     : {TextGenerator}", textGenerator);
        logger.LogInformation("*******************************************************************************");
    }
}
