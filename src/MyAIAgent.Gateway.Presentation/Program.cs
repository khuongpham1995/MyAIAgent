/**
 * MyAIAgent | Gateway · Presentation
 *
 * Bootstraps Gateway configuration, security, and HTTP routes for chat, conversations, and administration.
 */

using MediatR;
using MapsterMapper;
using Microsoft.AspNetCore.Http.Features;
using MyAIAgent.Common.Infrastructure.Extensions;
using Scalar.AspNetCore;
using MyAIAgent.Gateway.Application;
using MyAIAgent.Gateway.Application.Admin.Commands.UploadDocument;
using MyAIAgent.Gateway.Application.Admin.Queries.ListMcpTools;
using MyAIAgent.Gateway.Application.Agents.Commands.CreateAgent;
using MyAIAgent.Gateway.Application.Agents.Commands.DeleteAgent;
using MyAIAgent.Gateway.Application.Agents.Queries.ListAgents;
using MyAIAgent.Gateway.Application.Chat.Commands.ChatGateway;
using MyAIAgent.Gateway.Application.Chat.Commands.StreamChatGateway;
using MyAIAgent.Gateway.Presentation.PublicApi;
using MyAIAgent.Gateway.Application.Agents;
using MyAIAgent.Gateway.Application.Chat;
using MyAIAgent.Gateway.Infrastructure;
using MyAIAgent.Gateway.Presentation;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureAppConfiguration();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOpenApi("kyle-ai-agent");
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

app.MapGet("/health/live", () => Results.Ok());
app.MapGet("/health/ready", () => Results.Ok());

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Kyle's AI Agent";
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

string ResolveEntraUserId(System.Security.Claims.ClaimsPrincipal user) =>
    app.Environment.IsDevelopment()
        ? "development:anonymous"
        : $"{user.FindFirst("tid")!.Value}:{user.FindFirst("oid")!.Value}";

var chatEndpoint = app.MapPost("/chat", async (ChatGatewayRequestDto request, HttpContext context, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
{
    var userId = ResolveEntraUserId(context.User);
    var command = mapper.Map<ChatGatewayCommand>(new AuthenticatedChatGatewayRequest(request, userId));
    var result = await sender.Send(command, cancellationToken);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

var chatStreamEndpoint = app.MapPost("/chat/stream", async (ChatGatewayRequestDto request, HttpContext context, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
{
    var source = new AuthenticatedChatGatewayRequest(request, ResolveEntraUserId(context.User));
    var events = sender.CreateStream(mapper.Map<StreamChatGatewayCommand>(source), cancellationToken);
    return await events.ToServerSentEventsAsync(chatEvent => chatEvent.Type, cancellationToken);
});

var conversationRoutes = app.MapGroup(string.Empty);
conversationRoutes.MapConversationRoutes(ResolveEntraUserId);

if (!app.Environment.IsDevelopment())
{
    chatEndpoint.RequireAuthorization(GatewaySecurityExtensions.ChatPolicy);
    chatStreamEndpoint.RequireAuthorization(GatewaySecurityExtensions.ChatPolicy);
    conversationRoutes.RequireAuthorization(GatewaySecurityExtensions.ChatPolicy);
}

if (publicChat is not null)
    app.MapPublicChat(publicChat);

var admin = app.MapGroup("/admin");
if (!app.Environment.IsDevelopment())
    admin.RequireAuthorization(GatewaySecurityExtensions.AdminPolicy);

admin.MapPost("/documents", async (IFormFile file, ISender sender, CancellationToken cancellationToken, string? documentId = null) =>
{
    await using var stream = file.OpenReadStream();
    var command = new UploadDocumentCommand(file.FileName, stream, documentId);
    var result = await sender.Send(command, cancellationToken);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

admin.MapGet("/tools", async (ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new ListMcpToolsQuery(), cancellationToken);
    return Results.Ok(result);
});

admin.MapGet("/agents", async (ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new ListAgentsQuery(), cancellationToken);
    return Results.Ok(result);
});

admin.MapPost("/agents", async (CreateAgentRequestDto request, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(mapper.Map<CreateAgentCommand>(request), cancellationToken);
    return result.IsSuccess
        ? Results.Created($"/admin/agents/{result.Data}", result)
        : Results.BadRequest(result);
});

admin.MapDelete("/agents/{id}", async (string id, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new DeleteAgentCommand(id), cancellationToken);
    return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
});

await app.RunAsync();
