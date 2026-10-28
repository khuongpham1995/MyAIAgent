/**
 * MyAIAgent | Orchestrator · Presentation
 *
 * Bootstraps chat-service configuration and composes its service-authorized endpoint modules.
 */

using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Orchestrator.Application;
using MyAIAgent.Orchestrator.Infrastructure;
using MyAIAgent.Orchestrator.Presentation.Endpoints;
using MyAIAgent.Orchestrator.Presentation.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureAppConfiguration();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAzureAppConfigurationWhenConfigured(app.Configuration);
app.MapOrchestratorEndpoints();

await app.RunOrchestratorAsync(builder.Configuration);
