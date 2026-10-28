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
        var api = endpoints.MapGroup(string.Empty)
            .AddEndpointFilter(endpoints.ServiceProvider.GetRequiredService<HttpAuthEndpointFilter>());
        api.MapChatEndpoints()
            .MapConversationEndpoints()
            .MapAgentEndpoints();
        return endpoints;
    }
}
