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

    #region Private Method(s)
    
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
    }

    private static void LogStartupDiagnostics(
        ILogger logger,
        IServiceProvider services,
        KernelMemoryConfig config,
        string memoryType,
        int syncHandlersCount,
        int asyncHandlersCount)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        const double defaultMaxUploadMb = 30000000d / 1024 / 1024;

        if (config.ServiceAuthorization.Enabled
            && config.ServiceAuthorization.AccessKey1 == config.ServiceAuthorization.AccessKey2)
        {
            logger.LogError("Access keys 1 and 2 have the same value. Keys should be different to allow rotation.");
        }

        logger.LogInformation("*******************************************************************************");
        logger.LogInformation("* Environment         : {Environment}", string.IsNullOrEmpty(env) ? "WARNING: ASPNETCORE_ENVIRONMENT not defined" : env);
        logger.LogInformation("* Memory type         : {MemoryType}", memoryType);
        logger.LogInformation("* Pipeline handlers   : {Handlers}", $"{syncHandlersCount} synchronous / {asyncHandlersCount} asynchronous");
        logger.LogInformation("* Web service         : {WebServiceStatus}", config.Service.RunWebService ? "Enabled" : "Disabled");

        if (config.Service.RunWebService)
        {
            logger.LogInformation("* Web service auth    : {AuthStatus}", config.ServiceAuthorization.Enabled ? "Enabled" : "Disabled");
            logger.LogInformation("* Max HTTP req size   : {MaxSize} Mb", (config.Service.MaxUploadSizeMb ?? defaultMaxUploadMb).ToString("0.#", CultureInfo.CurrentCulture));
        }

        logger.LogInformation("* Memory Db           : {MemoryDb}", services.GetService<IMemoryDb>()?.GetType().FullName);
        logger.LogInformation("* Document storage    : {DocumentStorage}", services.GetService<IDocumentStorage>()?.GetType().FullName);
        logger.LogInformation("* Embedding generation: {EmbeddingGenerator}", services.GetService<ITextEmbeddingGenerator>()?.GetType().FullName);
        logger.LogInformation("* Text generation     : {TextGenerator}", services.GetService<ITextGenerator>()?.GetType().FullName);
        logger.LogInformation("*******************************************************************************");
    }

    #endregion
}
