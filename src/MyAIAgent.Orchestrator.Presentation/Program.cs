using MediatR;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Orchestrator.Application.Extensions;
using MyAIAgent.Orchestrator.Application.Handlers;
using MyAIAgent.Orchestrator.Domain.Models;
using MyAIAgent.Orchestrator.Infrastructure.Data;
using MyAIAgent.Orchestrator.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureAppConfiguration();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.UseAzureAppConfiguration();

await app.Services.EnsureCosmosDbCreatedAsync<OrchestratorDbContext>();

app.MapPost("/chat", async (ChatRequestDto request, ISender sender, CancellationToken cancellationToken) =>
{
    var command = new ChatCommand(request.SessionId, request.UserMessage);
    var result = await sender.Send(command, cancellationToken);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

await app.RunAsync();
