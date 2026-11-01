/**
 * MyAIAgent | Gateway · Presentation
 *
 * Maps visitor chat and conversation endpoints with server-derived ownership and public API authorization.
 */

using MyAIAgent.Common.Application.Models;
using MapsterMapper;
using MediatR;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Gateway.Application.Chat;
using MyAIAgent.Gateway.Application.Chat.Commands.StreamChatGateway;
using MyAIAgent.Gateway.Domain.Constants;

namespace MyAIAgent.Gateway.Presentation.PublicApi;

public static class PublicChatEndpoints
{
    public static IEndpointRouteBuilder MapPublicChatEndpoints(this IEndpointRouteBuilder endpoints, PublicChatSettings settings)
    {
        var group = endpoints.MapGroup(string.Empty)
            .RequireCors(GatewayConstant.PublicChat.CorsPolicy)
            .RequireAuthorization(GatewayConstant.PublicChat.Policy);

        group.MapGroup("/sse/public").MapPost("/chat", async (
            PublicChatRequestDto request,
            HttpContext context,
            ISender sender,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            var source = new PublicChatGatewayRequest(
                request,
                PublicChatExtensions.GetVisitorUserId(context.User),
                settings.AgentId);
            var events = sender.CreateStream(mapper.Map<StreamChatGatewayCommand>(source), cancellationToken);
            return await events.ToServerSentEventsAsync(chatEvent => chatEvent.Type, cancellationToken);
        }).WithName(PublicChatExtensions.StreamEndpointName)
            .Produces<ChatStreamEvent>(StatusCodes.Status200OK, "text/event-stream")
            .Produces<ResponseModel<ErrorResponseDto>>(StatusCodes.Status400BadRequest);

        group.MapGroup("/api/public").MapConversationEndpoints(PublicChatExtensions.GetVisitorUserId, "Public");

        return endpoints;
    }
}
