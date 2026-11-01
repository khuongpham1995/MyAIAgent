/**
 * MyAIAgent | Orchestrator · Presentation
 *
 * Maps anonymous Orchestrator process liveness and readiness checks.
 */

namespace MyAIAgent.Orchestrator.Presentation.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok());
        endpoints.MapGet("/health/ready", () => Results.Ok());
        return endpoints;
    }
}
