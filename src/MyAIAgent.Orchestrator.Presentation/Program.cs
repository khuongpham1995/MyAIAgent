using MediatR;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Orchestrator.Application.Extensions;
using MyAIAgent.Orchestrator.Application.Mappers;
using MyAIAgent.Orchestrator.Domain.Models;
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

await app.RunAsync();
