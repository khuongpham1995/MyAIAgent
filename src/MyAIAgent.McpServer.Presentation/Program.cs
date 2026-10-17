using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Common.Infrastructure.Filters;
using MyAIAgent.McpServer.Application;
using MyAIAgent.McpServer.Infrastructure;
using MyAIAgent.McpServer.Presentation.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureAppConfiguration();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddLogging();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAzureAppConfigurationWhenConfigured(app.Configuration);
app.MapGet("/health/live", () => Results.Ok());
app.MapGet("/health/ready", () => Results.Ok());
app.MapMcp().AddEndpointFilter(app.Services.GetRequiredService<HttpAuthEndpointFilter>());

await app.RunMcpServerAsync();
