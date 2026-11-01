/**
 * MyAIAgent | Orchestrator · Presentation
 *
 * Composes feature modules under service-key authorization while keeping health checks anonymous.
 */

using MyAIAgent.Common.Infrastructure.Filters;

namespace MyAIAgent.Orchestrator.Presentation.Endpoints;

public static class OrchestratorEndpoints
{
    public static IEndpointRouteBuilder MapOrchestratorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthEndpoints();
        var secured = endpoints.MapGroup(string.Empty)
            .AddEndpointFilter(endpoints.ServiceProvider.GetRequiredService<HttpAuthEndpointFilter>());
        secured.MapGroup("/api").MapChatEndpoints()
            .MapConversationEndpoints()
            .MapAgentEndpoints();
        secured.MapGroup("/sse").MapChatStreamingEndpoints();
        return endpoints;
    }
}
