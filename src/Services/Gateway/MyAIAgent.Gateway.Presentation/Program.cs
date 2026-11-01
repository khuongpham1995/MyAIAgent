/**
 * MyAIAgent | Gateway · Presentation
 *
 * Bootstraps Gateway configuration, security, and HTTP routes for chat, conversations, and administration.
 */

using Microsoft.AspNetCore.Http.Features;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Gateway.Application;
using MyAIAgent.Gateway.Infrastructure;
using MyAIAgent.Gateway.Presentation;
using MyAIAgent.Gateway.Presentation.PublicApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureAppConfiguration();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddGatewayOpenApi();
builder.Services.AddGatewaySecurity(builder.Configuration, builder.Environment.IsDevelopment());

var publicChat = PublicChatSettings.FromConfiguration(builder.Configuration, builder.Environment.IsDevelopment());
if (publicChat is not null)
    builder.Services.AddPublicChat(publicChat, builder.Environment.IsDevelopment());

// Allow large file uploads for document ingestion
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 512 * 1024 * 1024; // 512 MB
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseAzureAppConfigurationWhenConfigured(app.Configuration);

if (publicChat is not null)
{
    app.UsePublicChat(publicChat);
    app.UseAuthorization();
}
else if (!app.Environment.IsDevelopment())
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapDocumentationEndpoints()
    .MapGatewayEndpoints(publicChat);

await app.RunAsync();
