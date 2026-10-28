/**
 * MyAIAgent | Orchestrator · Presentation
 *
 * Maps owner-scoped conversation operations inside the service-authorized route group.
 */

using MediatR;
using MapsterMapper;
using MyAIAgent.Orchestrator.Application.Conversations;
using MyAIAgent.Orchestrator.Application.Conversations.Commands.DeleteConversation;
using MyAIAgent.Orchestrator.Application.Conversations.Commands.RenameConversation;
using MyAIAgent.Orchestrator.Application.Conversations.Queries.GetConversation;
using MyAIAgent.Orchestrator.Application.Conversations.Queries.ListConversations;

namespace MyAIAgent.Orchestrator.Presentation.Endpoints;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/conversations", async (string userId, ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        {
            var result = await sender.Send(new ListConversationsQuery(userId, page, pageSize), cancellationToken);
            return Results.Ok(result);
        });

        endpoints.MapGet("/conversations/{id}", async (string id, string userId, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetConversationQuery(id, userId), cancellationToken);
            return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
        });

        endpoints.MapPut("/conversations/{id}/title", async (string id, RenameConversationRequestDto request, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var command = mapper.Map<RenameConversationCommand>(new RenameConversationRequest(id, request));
            var result = await sender.Send(command, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
        });

        endpoints.MapDelete("/conversations/{id}", async (string id, string userId, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DeleteConversationCommand(id, userId), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result);
        });

        return endpoints;
    }
}
