using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.McpServer.Application.Extensions;
using MyAIAgent.McpServer.Infrastructure.Extensions;

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
app.UseAzureAppConfiguration();
app.MapMcp();

await app.RunAsync();
