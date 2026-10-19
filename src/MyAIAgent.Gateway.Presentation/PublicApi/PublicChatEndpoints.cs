using MapsterMapper;
using MediatR;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Gateway.Application.Chat;
using MyAIAgent.Gateway.Application.Chat.Commands.StreamChatGateway;
using MyAIAgent.Gateway.Domain.Constants;

namespace MyAIAgent.Gateway.Presentation.PublicApi;

public static class PublicChatEndpoints
{
    public static IEndpointRouteBuilder MapPublicChat(this IEndpointRouteBuilder app, PublicChatSettings settings)
    {
        var group = app.MapGroup("/public")
            .RequireCors(GatewayConstant.PublicChat.CorsPolicy)
            .RequireAuthorization(GatewayConstant.PublicChat.Policy);

        group.MapPost("/chat/stream", async (
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
        });

        group.MapConversationRoutes(PublicChatExtensions.GetVisitorUserId);

        return app;
    }
}
