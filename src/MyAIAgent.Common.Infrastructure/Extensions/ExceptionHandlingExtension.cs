/**
 * MyAIAgent | Common · Infrastructure
 *
 * Registers the shared exception handler and problem-details services at application startup.
 */

using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Infrastructure.Handlers;

namespace MyAIAgent.Common.Infrastructure.Extensions;

public static class ExceptionHandlingExtension
{
    public static IServiceCollection AddGlobalExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }
}
