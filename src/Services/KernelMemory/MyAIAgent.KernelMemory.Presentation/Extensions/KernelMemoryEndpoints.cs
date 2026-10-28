/**
 * MyAIAgent | KernelMemory · Presentation
 *
 * Maps the configured KernelMemory HTTP API and process health routes with the daemon's authorization filters.
 */

using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Service.AspNetCore;
using MyAIAgent.KernelMemory.Presentation.Filters;

namespace MyAIAgent.KernelMemory.Presentation.Extensions;

public static class KernelMemoryEndpoints
{
    public static IEndpointRouteBuilder MapKernelMemoryEndpoints(this IEndpointRouteBuilder endpoints, KernelMemoryConfig config, DateTimeOffset start)
    {
        if (!config.Service.RunWebService) return endpoints;

        endpoints.MapGet("/", () => Results.Ok(
            "Kernel Memory service is running. " +
            $"Uptime: {DateTimeOffset.UtcNow.ToUnixTimeSeconds() - start.ToUnixTimeSeconds()} secs " +
            $"- Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}"))
            .WithName("Status Checker");

        var loggerFactory = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var errorFilter = new HttpErrorsEndpointFilter(loggerFactory.CreateLogger<HttpErrorsEndpointFilter>());
        var authFilter = new HttpAuthEndpointFilter(config.ServiceAuthorization, loggerFactory.CreateLogger<HttpAuthEndpointFilter>());
        endpoints.AddKernelMemoryEndpoints("/", config, [errorFilter, authFilter]);

        endpoints.MapGet("/health", () => Results.Ok("Service is healthy."))
            .WithName("Health Checker");
        endpoints.MapGet("/health/live", () => Results.Ok());
        endpoints.MapGet("/health/ready", () => Results.Ok());

        return endpoints;
    }
}
