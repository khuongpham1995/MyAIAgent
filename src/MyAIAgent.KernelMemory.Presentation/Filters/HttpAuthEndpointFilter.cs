using Microsoft.KernelMemory.Configuration;

namespace MyAIAgent.KernelMemory.Presentation.Filters;

public sealed class HttpAuthEndpointFilter : IEndpointFilter
{
    private readonly ServiceAuthorizationConfig _config;
    public HttpAuthEndpointFilter(ServiceAuthorizationConfig config)
    {
        config.Validate();
        _config = config;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (!_config.Enabled) return await next(context);
        if (!context.HttpContext.Request.Headers.TryGetValue(_config.HttpHeaderName, out var apiKey))
        {
            return Results.Problem(detail: "Missing API Key HTTP header", statusCode: 401);
        }

        if (!string.Equals(apiKey, _config.AccessKey1, StringComparison.Ordinal)
            && !string.Equals(apiKey, _config.AccessKey2, StringComparison.Ordinal))
        {
            return Results.Problem(detail: "Invalid API Key", statusCode: 403);
        }

        return await next(context);
    }
}
