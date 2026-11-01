/**
 * MyAIAgent | Gateway · Presentation
 *
 * Maps anonymous Gateway liveness and readiness routes with their stable OpenAPI operation names.
 */

namespace MyAIAgent.Gateway.Presentation;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok()).WithName("CheckConnection");
        endpoints.MapGet("/health/ready", () => Results.Ok()).WithName("CheckReadiness");
        return endpoints;
    }
}
