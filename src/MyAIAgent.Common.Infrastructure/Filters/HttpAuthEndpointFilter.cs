/**
 * MyAIAgent | Common · Infrastructure
 *
 * Checks active or rotation service keys and rejects missing or invalid credentials when authorization is enabled.
 */

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Common.Infrastructure.Filters;

public sealed class HttpAuthEndpointFilter(
    IOptions<ServiceAuthorizationModel> options,
    ILogger<HttpAuthEndpointFilter> logger) : IEndpointFilter
{
    private readonly ServiceAuthorizationModel config = Validated(options.Value);

    private static ServiceAuthorizationModel Validated(ServiceAuthorizationModel model)
    {
        if (model.Enabled && string.IsNullOrWhiteSpace(model.AccessKey1))
            throw new InvalidOperationException(
                "ServiceAuthorization.AccessKey1 must not be empty when auth is enabled.");

        if (model.Enabled && string.IsNullOrWhiteSpace(model.AccessKey2))
            throw new InvalidOperationException(
                "ServiceAuthorization.AccessKey2 must not be empty when auth is enabled.");

        return model;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (!config.Enabled)
            return await next(context);

        if (!context.HttpContext.Request.Headers.TryGetValue(config.HttpHeaderName, out var apiKey))
        {
            logger.LogWarning("Missing API key header on {Path}", context.HttpContext.Request.Path);
            return Results.Problem(detail: "Missing API key HTTP header", statusCode: 401);
        }

        if (config.AccessKey1.Equals(apiKey, StringComparison.Ordinal)
            || config.AccessKey2.Equals(apiKey, StringComparison.Ordinal))
        {
            return await next(context);
        }

        logger.LogWarning("Invalid API key on {Path}", context.HttpContext.Request.Path);
        return Results.Problem(detail: "Invalid API key", statusCode: 403);
    }
}
