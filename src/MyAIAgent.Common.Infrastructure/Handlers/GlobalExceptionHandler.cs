using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Common.Infrastructure.Handlers;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            logger.LogInformation("Request cancelled for {ExceptionType}", exception.GetType().Name);
            return true;
        }

        if (exception is ValidationException validationException)
        {
            logger.LogWarning("Validation failed: {ExceptionType}", exception.GetType().Name);

            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => string.Join(" | ", g.Select(e => e.ErrorMessage)));

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(
                ResponseModel<object>.Fail(errors, message: "Validation failed"),
                cancellationToken);

            return true;
        }

        logger.LogError(exception, "Unhandled exception of type {ExceptionType}", exception.GetType().Name);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            ResponseModel<object>.Fail(message: "An unexpected error occurred"),
            cancellationToken);

        return true;
    }
}
