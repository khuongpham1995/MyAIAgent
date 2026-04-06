using MediatR;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Orchestrator.Application.Extensions;
using MyAIAgent.Orchestrator.Application.Handlers.Agents;
using MyAIAgent.Orchestrator.Application.Mappers;
using MyAIAgent.Orchestrator.Application.Models;
using MyAIAgent.Orchestrator.Infrastructure.Data;
using MyAIAgent.Orchestrator.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureAppConfiguration();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAzureAppConfiguration();

await app.Services.EnsureCosmosDbCreatedAsync<OrchestratorDbContext>();

app.MapPost("/chat", async (ChatRequestDto request, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(request.ToCommand(), cancellationToken);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapGet("/agents", async (ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new ListAgentsQuery(), cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/agents", async (CreateAgentRequestDto request, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(request.ToCommand(), cancellationToken);
    return result.IsSuccess
        ? Results.Created($"/agents/{result.Data}", result)
        : Results.BadRequest(result);
});

app.MapDelete("/agents/{id}", async (string id, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new DeleteAgentCommand(id), cancellationToken);
    return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
});

await app.RunAsync();
