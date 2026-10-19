using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.Diagnostics;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.KernelMemory.Application;
using MyAIAgent.KernelMemory.Infrastructure.Data;
using MyAIAgent.KernelMemory.Infrastructure;
using MyAIAgent.KernelMemory.Presentation.Extensions;
using MyAIAgent.KernelMemory.Infrastructure.Extensions;
using static MyAIAgent.KernelMemory.Infrastructure.Extensions.KernelMemoryPipelineExtension;

SensitiveDataLogger.Enabled = false;

var start = DateTimeOffset.UtcNow;
var asyncHandlersCount = 0;
var syncHandlersCount = 0;
var memoryType = string.Empty;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureAppConfiguration();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddGlobalExceptionHandling();

var config = builder.Configuration.GetSection("KernelMemory").Get<KernelMemoryConfig>()
    ?? throw new InvalidOperationException("KernelMemory configuration section is missing.");

if (!builder.Environment.IsDevelopment()
    && !string.Equals(config.DocumentStorageType, "AzureBlobs", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("KernelMemory:DocumentStorageType must be AzureBlobs in production.");

builder.AddKernelMemory(
    memoryBuilder =>
    {
        // In-process orchestration relies on KernelMemory's default pipeline handlers (extract, partition, embed, save).
        memoryBuilder.AddConfiguredProviders(builder.Configuration, config);
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

app.UseExceptionHandler();
app.UseAzureAppConfigurationWhenConfigured(app.Configuration);

await app.RunKernelMemoryAsync(config, start, memoryType, syncHandlersCount, asyncHandlersCount);
