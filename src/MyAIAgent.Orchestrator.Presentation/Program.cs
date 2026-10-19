using MediatR;
using MapsterMapper;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Orchestrator.Application;
using MyAIAgent.Orchestrator.Application.Agents.Commands.CreateAgent;
using MyAIAgent.Orchestrator.Application.Agents.Commands.DeleteAgent;
using MyAIAgent.Orchestrator.Application.Agents.Queries.ListAgents;
using MyAIAgent.Orchestrator.Application.Chat.Commands.Chat;
using MyAIAgent.Orchestrator.Application.Chat.Commands.StreamChat;
using MyAIAgent.Orchestrator.Application.Conversations;
using MyAIAgent.Orchestrator.Application.Conversations.Commands.DeleteConversation;
using MyAIAgent.Orchestrator.Application.Conversations.Commands.RenameConversation;
using MyAIAgent.Orchestrator.Application.Conversations.Queries.GetConversation;
using MyAIAgent.Orchestrator.Application.Conversations.Queries.ListConversations;
using MyAIAgent.Orchestrator.Application.Agents;
using MyAIAgent.Orchestrator.Application.Chat;
using MyAIAgent.Orchestrator.Infrastructure.Data;
using MyAIAgent.Common.Infrastructure.Filters;
using MyAIAgent.Orchestrator.Infrastructure;
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
app.MapGet("/health/live", () => Results.Ok());
app.MapGet("/health/ready", () => Results.Ok());


var api = app.MapGroup("").AddEndpointFilter(app.Services.GetRequiredService<HttpAuthEndpointFilter>());

api.MapPost("/chat", async (ChatRequestDto request, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(mapper.Map<ChatCommand>(request), cancellationToken);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

api.MapPost("/chat/stream", async (ChatRequestDto request, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
{
    var events = sender.CreateStream(mapper.Map<StreamChatCommand>(request), cancellationToken);
    return await events.ToServerSentEventsAsync(chatEvent => chatEvent.Type, cancellationToken);
});

api.MapGet("/conversations", async (string userId, ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
{
    var result = await sender.Send(new ListConversationsQuery(userId, page, pageSize), cancellationToken);
    return Results.Ok(result);
});

api.MapGet("/conversations/{id}", async (string id, string userId, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetConversationQuery(id, userId), cancellationToken);
    return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
});

api.MapPut("/conversations/{id}/title", async (string id, RenameConversationRequestDto request, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
{
    var command = mapper.Map<RenameConversationCommand>(new RenameConversationRequest(id, request));
    var result = await sender.Send(command, cancellationToken);
    return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
});

api.MapDelete("/conversations/{id}", async (string id, string userId, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new DeleteConversationCommand(id, userId), cancellationToken);
    return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
});

api.MapGet("/agents", async (ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new ListAgentsQuery(), cancellationToken);
    return Results.Ok(result);
});

api.MapPost("/agents", async (CreateAgentRequestDto request, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(mapper.Map<CreateAgentCommand>(request), cancellationToken);
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
