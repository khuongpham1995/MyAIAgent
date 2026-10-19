using System.Security.Claims;
using MapsterMapper;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Application.Conversations;
using MyAIAgent.Gateway.Application.Conversations.Commands.DeleteConversation;
using MyAIAgent.Gateway.Application.Conversations.Commands.RenameConversation;
using MyAIAgent.Gateway.Application.Conversations.Queries.GetConversation;
using MyAIAgent.Gateway.Application.Conversations.Queries.ListConversations;
using MyAIAgent.Gateway.Domain.Constants;

namespace MyAIAgent.Gateway.Presentation.PublicApi;

public static class ConversationEndpoints
{
    // Shared by the visitor and Entra surfaces; only the way the owner id is derived differs.
    public static RouteGroupBuilder MapConversationRoutes(
        this RouteGroupBuilder group,
        Func<ClaimsPrincipal, string> resolveUserId)
    {
        group.MapGet("/conversations", async (
            HttpContext context, ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        {
            var query = new ListConversationsQuery(resolveUserId(context.User), page, pageSize);
            var result = await sender.Send(query, cancellationToken);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/conversations/{id}", async (
            string id, HttpContext context, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetConversationQuery(id, resolveUserId(context.User)), cancellationToken);
            return ToResult(result);
        });

        group.MapPut("/conversations/{id}/title", async (
            string id,
            RenameConversationRequestDto body,
            HttpContext context,
            ISender sender,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            var command = mapper.Map<RenameConversationCommand>(
                new RenameConversationRequest(id, resolveUserId(context.User), body));
            var result = await sender.Send(command, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : ToResult(result);
        });

        group.MapDelete("/conversations/{id}", async (
            string id, HttpContext context, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DeleteConversationCommand(id, resolveUserId(context.User)), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : ToResult(result);
        });

        return group;
    }

    private static IResult ToResult<T>(ResponseModel<T> result) =>
        result.IsSuccess ? Results.Ok(result)
        : result.Message == GatewayConstant.Errors.ConversationNotFound ? Results.NotFound(result)
        : Results.BadRequest(result);
}
