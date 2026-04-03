using Microsoft.KernelMemory.Configuration;

namespace MyAIAgent.KernelMemory.Presentation.Filters;

public sealed class HttpAuthEndpointFilter(
    ServiceAuthorizationConfig config,
    ILogger<HttpAuthEndpointFilter> logger) : IEndpointFilter
{
    private readonly ServiceAuthorizationConfig _config = Validated(config);

    private static ServiceAuthorizationConfig Validated(ServiceAuthorizationConfig config)
    {
        config.Validate();
        return config;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (!_config.Enabled) return await next(context);

        if (!context.HttpContext.Request.Headers.TryGetValue(_config.HttpHeaderName, out var apiKey))
        {
            logger.LogWarning("Missing API Key header on {Path}", context.HttpContext.Request.Path);
            return Results.Problem(detail: "Missing API Key HTTP header", statusCode: 401);
        }

        if (_config.AccessKey1.Equals(apiKey, StringComparison.Ordinal)
            || _config.AccessKey2.Equals(apiKey, StringComparison.Ordinal))
        {
            return await next(context);
        }

        logger.LogWarning("Invalid API Key on {Path}", context.HttpContext.Request.Path);
        return Results.Problem(detail: "Invalid API Key", statusCode: 403);

    }
}
