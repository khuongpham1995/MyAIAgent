using MediatR;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Orchestrator.Application.Extensions;
using MyAIAgent.Orchestrator.Application.Handlers.Agents;
using MyAIAgent.Orchestrator.Application.Mappers;
using MyAIAgent.Orchestrator.Application.Models;
using MyAIAgent.Orchestrator.Infrastructure.Data;
using MyAIAgent.Common.Infrastructure.Filters;
using MyAIAgent.Orchestrator.Infrastructure.Extensions;
using MyAIAgent.Orchestrator.Presentation.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureAppConfiguration();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAzureAppConfiguration();

if (app.Environment.IsDevelopment())
    await app.Services.EnsureDbCreatedAsync<OrchestratorDbContext>();

var api = app.MapGroup("").AddEndpointFilter(app.Services.GetRequiredService<HttpAuthEndpointFilter>());

api.MapPost("/chat", async (ChatRequestDto request, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(request.ToCommand(), cancellationToken);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

api.MapGet("/agents", async (ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new ListAgentsQuery(), cancellationToken);
    return Results.Ok(result);
});

api.MapPost("/agents", async (CreateAgentRequestDto request, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(request.ToCommand(), cancellationToken);
    return result.IsSuccess
        ? Results.Created($"/agents/{result.Data}", result)
        : Results.BadRequest(result);
});

api.MapDelete("/agents/{id}", async (string id, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new DeleteAgentCommand(id), cancellationToken);
    return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
});

await app.RunOrchestratorAsync(builder.Configuration);
