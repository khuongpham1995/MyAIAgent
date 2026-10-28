/**
 * MyAIAgent | Gateway · Presentation
 *
 * Maps Gateway chat and streaming handlers inside the caller-authorized route group.
 */

using MyAIAgent.Common.Application.Models;
using System.Security.Claims;
using MapsterMapper;
using MediatR;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Gateway.Application.Chat;
using MyAIAgent.Gateway.Application.Chat.Commands.ChatGateway;
using MyAIAgent.Gateway.Application.Chat.Commands.StreamChatGateway;

namespace MyAIAgent.Gateway.Presentation;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints, Func<ClaimsPrincipal, string> resolveUserId)
    {
        endpoints.MapPost("/chat", async (ChatGatewayRequestDto request, HttpContext context, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var userId = resolveUserId(context.User);
            var command = mapper.Map<ChatGatewayCommand>(new AuthenticatedChatGatewayRequest(request, userId));
            var result = await sender.Send(command, cancellationToken);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result.ToErrorResponse(StatusCodes.Status400BadRequest));
        }).WithName("Chat")
            .Produces<ResponseModel<ChatGatewayResponse>>()
            .Produces<ResponseModel<ErrorResponseDto>>(StatusCodes.Status400BadRequest);

        endpoints.MapPost("/chat/stream", async (ChatGatewayRequestDto request, HttpContext context, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var source = new AuthenticatedChatGatewayRequest(request, resolveUserId(context.User));
            var events = sender.CreateStream(mapper.Map<StreamChatGatewayCommand>(source), cancellationToken);
            return await events.ToServerSentEventsAsync(chatEvent => chatEvent.Type, cancellationToken);
        }).WithName("StreamChat")
            .Produces<ChatStreamEvent>(StatusCodes.Status200OK, "text/event-stream")
            .Produces<ResponseModel<ErrorResponseDto>>(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}
