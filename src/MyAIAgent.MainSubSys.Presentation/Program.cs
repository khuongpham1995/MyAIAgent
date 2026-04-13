using MediatR;
using Microsoft.AspNetCore.Http.Features;
using MyAIAgent.Common.Infrastructure.Extensions;
using Scalar.AspNetCore;
using MyAIAgent.MainSubSys.Application.Extensions;
using MyAIAgent.MainSubSys.Application.Handlers.Admin;
using MyAIAgent.MainSubSys.Application.Handlers.Agent;
using MyAIAgent.MainSubSys.Application.Mappers;
using MyAIAgent.MainSubSys.Application.Models;
using MyAIAgent.MainSubSys.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureAppConfiguration();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOpenApi("kyle-ai-agent");

// Allow large file uploads for document ingestion
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 512 * 1024 * 1024; // 512 MB
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseAzureAppConfiguration();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "Kyle's AI Agent";
    options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.MapPost("/chat", async (ChatGatewayRequestDto request, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(request.ToCommand(), cancellationToken);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/admin/documents", async (IFormFile file, ISender sender, CancellationToken cancellationToken, string? documentId = null) =>
{
    await using var stream = file.OpenReadStream();
    var command = new UploadDocumentCommand(file.FileName, stream, documentId);
    var result = await sender.Send(command, cancellationToken);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapGet("/admin/tools", async (ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new ListMcpToolsQuery(), cancellationToken);
    return Results.Ok(result);
});

app.MapGet("/admin/agents", async (ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new ListAgentsQuery(), cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/admin/agents", async (CreateAgentRequestDto request, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(request.ToCommand(), cancellationToken);
    return result.IsSuccess
        ? Results.Created($"/admin/agents/{result.Data}", result)
        : Results.BadRequest(result);
});

app.MapDelete("/admin/agents/{id}", async (string id, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new DeleteAgentCommand(id), cancellationToken);
    return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
});

await app.RunAsync();
