/**
 * MyAIAgent | KernelMemory · Presentation
 *
 * Preserves upload-size HTTP errors and logs unexpected KernelMemory endpoint exceptions before rethrowing.
 */

namespace MyAIAgent.KernelMemory.Presentation.Filters;

public sealed class HttpErrorsEndpointFilter(ILogger<HttpErrorsEndpointFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (BadHttpRequestException e) when (e.StatusCode == 413)
        {
            logger.LogWarning("Request payload too large on {Path}", context.HttpContext.Request.Path);
            return Results.Problem(
                statusCode: e.StatusCode,
                detail: e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unhandled exception on {Path}", context.HttpContext.Request.Path);
            throw;
        }
    }
}
