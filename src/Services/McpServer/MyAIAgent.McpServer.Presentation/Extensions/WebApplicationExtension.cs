/**
 * MyAIAgent | McpServer · Presentation
 *
 * Protects raw MCP transport dispatch and reports nonsecret startup diagnostics.
 */

using MyAIAgent.Common.Infrastructure.Configuration;
using System.Reflection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using MyAIAgent.Common.Infrastructure.Filters;

namespace MyAIAgent.McpServer.Presentation.Extensions;

public static class WebApplicationExtension
{
    public static WebApplication UseMcpServiceAuthorization(this WebApplication app)
    {
        var authorization = app.Services.GetRequiredService<HttpAuthEndpointFilter>();
        // Some SDK transport methods are raw request delegates or return 405 in stateless mode.
        // Authorize before protocol dispatch, including requests that cannot run an endpoint filter.
        app.Use(async (context, next) =>
        {
            if ((context.Request.Path == "/" || context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
                && authorization.Authorize(context) is { } rejection)
            {
                await rejection.ExecuteAsync(context);
                return;
            }

            await next(context);
        });
        return app;
    }

    internal static async Task RunMcpServerAsync(this WebApplication app)
    {
        var auth = app.Services.GetRequiredService<IOptions<ServiceAuthorizationModel>>().Value;
        LogStartupDiagnostics(app.Logger, app.Environment, auth);

        try
        {
            await app.RunAsync();
        }
        catch (IOException e)
        {
            app.Logger.LogError("I/O error: {ErrorMessage}", e.Message);
            Environment.Exit(-1);
        }
    }

    private static void LogStartupDiagnostics(
        ILogger logger,
        IHostEnvironment environment,
        ServiceAuthorizationModel auth)
    {
        var tools = DiscoverTools();

        logger.LogInformation(
            "MCP configured for {Environment}: Streamable HTTP, authorization enabled: {AuthorizationEnabled}, {ToolCount} tools: {ToolNames}",
            environment.EnvironmentName, auth.Enabled, tools.Count, string.Join(", ", tools));
    }

    private static List<string> DiscoverTools()
        => [.. Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods())
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>())
            .Where(a => a is not null)
            .Select(a => a!.Name ?? "unnamed")];
}
