using MediatR;
using MapsterMapper;
using Microsoft.AspNetCore.Http.Features;
using MyAIAgent.Common.Infrastructure.Extensions;
using Scalar.AspNetCore;
using MyAIAgent.MainSubSys.Application.Extensions;
using MyAIAgent.MainSubSys.Application.Admin.Commands.UploadDocument;
using MyAIAgent.MainSubSys.Application.Admin.Queries.ListMcpTools;
using MyAIAgent.MainSubSys.Application.Agents.Commands.CreateAgent;
using MyAIAgent.MainSubSys.Application.Agents.Commands.DeleteAgent;
using MyAIAgent.MainSubSys.Application.Agents.Queries.ListAgents;
using MyAIAgent.MainSubSys.Application.Chat.Commands.ChatGateway;
using MyAIAgent.MainSubSys.Application.Models;
using MyAIAgent.MainSubSys.Infrastructure.Extensions;
using MyAIAgent.MainSubSys.Presentation;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureAppConfiguration();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOpenApi("kyle-ai-agent");
builder.Services.AddGatewaySecurity(builder.Configuration, builder.Environment.IsDevelopment());

// Allow large file uploads for document ingestion
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 512 * 1024 * 1024; // 512 MB
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseAzureAppConfigurationWhenConfigured(app.Configuration);

if (!app.Environment.IsDevelopment())
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

var chatEndpoint = app.MapPost("/chat", async (ChatGatewayRequestDto request, HttpContext context, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
{
    var userId = app.Environment.IsDevelopment()
        ? "development:anonymous"
        : $"{context.User.FindFirst("tid")!.Value}:{context.User.FindFirst("oid")!.Value}";
    var command = mapper.Map<ChatGatewayCommand>(new AuthenticatedChatGatewayRequest(request, userId));
    var result = await sender.Send(command, cancellationToken);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

if (!app.Environment.IsDevelopment())
    chatEndpoint.RequireAuthorization(GatewaySecurityExtensions.ChatPolicy);

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
