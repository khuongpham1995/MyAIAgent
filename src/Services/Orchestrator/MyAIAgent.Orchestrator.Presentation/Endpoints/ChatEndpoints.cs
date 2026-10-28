/**
 * MyAIAgent | Orchestrator · Presentation
 *
 * Maps chat and SSE handlers inside the service-authorized route group.
 */

using MediatR;
using MapsterMapper;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Orchestrator.Application.Chat;
using MyAIAgent.Orchestrator.Application.Chat.Commands.Chat;
using MyAIAgent.Orchestrator.Application.Chat.Commands.StreamChat;

namespace MyAIAgent.Orchestrator.Presentation.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/chat", async (ChatRequestDto request, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(mapper.Map<ChatCommand>(request), cancellationToken);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        endpoints.MapPost("/chat/stream", async (ChatRequestDto request, ISender sender, IMapper mapper, CancellationToken cancellationToken) =>
        {
            var events = sender.CreateStream(mapper.Map<StreamChatCommand>(request), cancellationToken);
            return await events.ToServerSentEventsAsync(chatEvent => chatEvent.Type, cancellationToken);
        });

        return endpoints;
    }
}
