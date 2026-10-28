/**
 * MyAIAgent | Orchestrator · Presentation
 *
 * Maps agent administration handlers inside the service-authorized route group.
 */

using MediatR;
using MapsterMapper;
using MyAIAgent.Orchestrator.Application.Agents;
using MyAIAgent.Orchestrator.Application.Agents.Commands.CreateAgent;
using MyAIAgent.Orchestrator.Application.Agents.Commands.DeleteAgent;
using MyAIAgent.Orchestrator.Application.Agents.Queries.ListAgents;

namespace MyAIAgent.Orchestrator.Presentation.Endpoints;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/agents", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ListAgentsQuery(), cancellationToken);
            return Results.Ok(result);
        });

        endpoints.MapPost("/agents", async (CreateAgentRequestDto request, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(mapper.Map<CreateAgentCommand>(request), cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/agents/{result.Data}", result)
                : Results.BadRequest(result);
        });

        endpoints.MapDelete("/agents/{id}", async (string id, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DeleteAgentCommand(id), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
        });

        return endpoints;
    }
}
