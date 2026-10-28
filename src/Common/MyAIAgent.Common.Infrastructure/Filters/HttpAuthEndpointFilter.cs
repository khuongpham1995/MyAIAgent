/**
 * MyAIAgent | Common · Infrastructure
 *
 * Checks active or rotation service keys and rejects missing or invalid credentials when authorization is enabled.
 */

using MyAIAgent.Common.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public IResult? Authorize(HttpContext context)
    {
        if (!config.Enabled)
            return null;

        if (!context.Request.Headers.TryGetValue(config.HttpHeaderName, out var apiKey))
        {
            logger.LogWarning("Missing API key header on {Path}", context.Request.Path);
            return Results.Problem(detail: "Missing API key HTTP header", statusCode: 401);
        }

        if (config.AccessKey1.Equals(apiKey, StringComparison.Ordinal)
            || config.AccessKey2.Equals(apiKey, StringComparison.Ordinal))
        {
            return null;
        }

        logger.LogWarning("Invalid API key on {Path}", context.Request.Path);
        return Results.Problem(detail: "Invalid API key", statusCode: 403);
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next) =>
        Authorize(context.HttpContext) is { } rejection ? rejection : await next(context);
}
