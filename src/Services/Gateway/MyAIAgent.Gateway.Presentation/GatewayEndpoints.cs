/**
 * MyAIAgent | Gateway · Presentation
 *
 * Composes Gateway endpoint modules and their authorization groups for runtime and OpenAPI tests.
 */

using System.Security.Claims;
using MyAIAgent.Gateway.Presentation.PublicApi;

namespace MyAIAgent.Gateway.Presentation;

public static class GatewayEndpoints
{
    public static IEndpointRouteBuilder MapGatewayEndpoints(this IEndpointRouteBuilder endpoints, PublicChatSettings? publicChat = null)
    {
        var environment = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        endpoints.MapHealthEndpoints();

        string ResolveEntraUserId(ClaimsPrincipal user) =>
            environment.IsDevelopment()
                ? "development:anonymous"
                : $"{user.FindFirst("tid")!.Value}:{user.FindFirst("oid")!.Value}";

        var chat = endpoints.MapGroup(string.Empty);
        if (!environment.IsDevelopment())
            chat.RequireAuthorization(GatewaySecurityExtensions.ChatPolicy);
        chat.MapGroup("/api").MapChatEndpoints(ResolveEntraUserId)
            .MapConversationEndpoints(ResolveEntraUserId);
        chat.MapGroup("/sse").MapChatStreamingEndpoints(ResolveEntraUserId);

        if (publicChat is not null)
            endpoints.MapPublicChatEndpoints(publicChat);

        var admin = endpoints.MapGroup("/api/admin");
        if (!environment.IsDevelopment())
            admin.RequireAuthorization(GatewaySecurityExtensions.AdminPolicy);
        admin.MapAdminEndpoints();

        return endpoints;
    }
}
