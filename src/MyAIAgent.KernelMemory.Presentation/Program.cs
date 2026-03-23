using System.Globalization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.DocumentStorage;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Service.AspNetCore;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.KernelMemory.Application.Extensions;
using MyAIAgent.KernelMemory.Infrastructure.Data;
using MyAIAgent.KernelMemory.Infrastructure.Extensions;
using MyAIAgent.KernelMemory.Presentation.Middleware;
using static MyAIAgent.KernelMemory.Infrastructure.Extensions.KernelMemoryPipelineExtension;

SensitiveDataLogger.Enabled = false;

var start = DateTimeOffset.UtcNow;
var asyncHandlersCount = 0;
var syncHandlersCount = 0;
var memoryType = string.Empty;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureAppConfiguration();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var config = builder.Configuration.GetSection("KernelMemory").Get<KernelMemoryConfig>()
    ?? throw new InvalidOperationException("KernelMemory configuration section is missing.");

builder.AddKernelMemory(
    memoryBuilder =>
    {
        memoryBuilder.WithoutDefaultHandlers();
        asyncHandlersCount = AddHandlersAsHostedServices(config, memoryBuilder, builder.Services);
    },
    memory =>
    {
        syncHandlersCount = AddHandlersToServerlessMemory(config, memory);
        memoryType = (memory is MemoryServerless ? "Sync - " : "Async - ") + memory.GetType().FullName;
    },
    services =>
    {
        var maxBytes = config.Service.GetMaxUploadSizeInBytes();
        if (!maxBytes.HasValue) return;
        services.Configure<KestrelServerOptions>(x => { x.Limits.MaxRequestBodySize = maxBytes.Value; });
        services.Configure<FormOptions>(x =>
        {
            x.MultipartBodyLengthLimit = maxBytes.Value;
            x.ValueLengthLimit = int.MaxValue;
        });
    });

var app = builder.Build();

app.UseAzureAppConfiguration();
app.UseKernelMemoryApiKey();

await app.Services.EnsureCosmosDbCreatedAsync<KernelMemoryDbContext>();

if (config.Service.RunWebService)
{
    app.MapGet("/", () => Results.Ok(
        "Kernel Memory service is running. " +
        $"Uptime: {DateTimeOffset.UtcNow.ToUnixTimeSeconds() - start.ToUnixTimeSeconds()} secs " +
        $"- Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}"))
        .WithName("ServiceStatus");

    app.AddKernelMemoryEndpoints();

    app.MapGet("/health", () => Results.Ok("Service is healthy."))
        .WithName("ServiceHealth");
}

var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
const double DefaultMaxUploadMb = 30000000d / 1024 / 1024;

if (config.ServiceAuthorization.Enabled
    && config.ServiceAuthorization.AccessKey1 == config.ServiceAuthorization.AccessKey2)
{
    app.Logger.LogError("Access keys 1 and 2 have the same value. Keys should be different to allow rotation.");
}

app.Logger.LogInformation("*******************************************************************************");
app.Logger.LogInformation("* Environment         : {Environment}", string.IsNullOrEmpty(env) ? "WARNING: ASPNETCORE_ENVIRONMENT not defined" : env);
app.Logger.LogInformation("* Memory type         : {MemoryType}", memoryType);
app.Logger.LogInformation("* Pipeline handlers   : {Handlers}", $"{syncHandlersCount} synchronous / {asyncHandlersCount} asynchronous");
app.Logger.LogInformation("* Web service         : {WebServiceStatus}", config.Service.RunWebService ? "Enabled" : "Disabled");

if (config.Service.RunWebService)
{
    app.Logger.LogInformation("* Web service auth    : {AuthStatus}", config.ServiceAuthorization.Enabled ? "Enabled" : "Disabled");
    app.Logger.LogInformation("* Max HTTP req size   : {MaxSize} Mb", (config.Service.MaxUploadSizeMb ?? DefaultMaxUploadMb).ToString("0.#", CultureInfo.CurrentCulture));
}

app.Logger.LogInformation("* Memory Db           : {MemoryDb}", app.Services.GetService<IMemoryDb>()?.GetType().FullName);
app.Logger.LogInformation("* Document storage    : {DocumentStorage}", app.Services.GetService<IDocumentStorage>()?.GetType().FullName);
app.Logger.LogInformation("* Embedding generation: {EmbeddingGenerator}", app.Services.GetService<ITextEmbeddingGenerator>()?.GetType().FullName);
app.Logger.LogInformation("* Text generation     : {TextGenerator}", app.Services.GetService<ITextGenerator>()?.GetType().FullName);
app.Logger.LogInformation("*******************************************************************************");

try
{
    await app.RunAsync();
}
catch (IOException e)
{
    app.Logger.LogError("I/O error: {ErrorMessage}", e.Message);
    Environment.Exit(-1);
}

