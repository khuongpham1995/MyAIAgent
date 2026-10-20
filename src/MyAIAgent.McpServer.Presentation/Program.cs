/**
 * MyAIAgent | McpServer · Presentation
 *
 * Bootstraps MCP HTTP transport, discovers tool classes, and maps tool and health endpoints.
 */

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
var mcp = app.MapGroup(string.Empty)
    .AddEndpointFilter(app.Services.GetRequiredService<HttpAuthEndpointFilter>());
mcp.MapMcp("/mcp");
// Retain the original root endpoint for agents that already store the service base URL.
mcp.MapMcp();

await app.RunMcpServerAsync();
