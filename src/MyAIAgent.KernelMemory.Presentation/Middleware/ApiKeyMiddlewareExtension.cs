using Microsoft.Extensions.Options;
using MyAIAgent.KernelMemory.Domain.Options;

namespace MyAIAgent.KernelMemory.Presentation.Middleware;

public static class ApiKeyMiddlewareExtension
{
    public static void UseKernelMemoryApiKey(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<ServiceAuthorizationOptions>>().Value;

        if (!options.Enabled) return;

        if (!string.IsNullOrEmpty(options.AccessKey1) && options.AccessKey1 == options.AccessKey2)
            app.Logger.LogError("KM Web Service: Access keys 1 and 2 have the same value. Keys should be different to allow rotation.");

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/health"))
            {
                await next(context);
                return;
            }

            var header = context.Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;
            var key = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? header["Bearer ".Length..].Trim()
                : header.Trim();

            if (!string.IsNullOrEmpty(key) && (key == options.AccessKey1 || key == options.AccessKey2))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
        });
    }
}
