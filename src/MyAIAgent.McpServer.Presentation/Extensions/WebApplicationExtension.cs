using System.Reflection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.McpServer.Presentation.Extensions;

internal static class WebApplicationExtension
{
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
        var env = environment.EnvironmentName;
        var authStatus = auth.Enabled ? "Enabled" : "Disabled";
        var tools = DiscoverTools();

        logger.LogInformation("*******************************************************************************");
        logger.LogInformation("* Environment         : {Environment}", env);
        logger.LogInformation("* Transport           : HTTP (SSE + POST)");
        logger.LogInformation("* Auth                : {AuthStatus}", authStatus);
        logger.LogInformation("* Registered tools    : {ToolCount}", tools.Count);

        foreach (var tool in tools)
            logger.LogInformation("*   - {ToolName}", tool);

        logger.LogInformation("*******************************************************************************");
    }

    private static List<string> DiscoverTools()
        => Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(t => t.GetMethods())
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>())
            .Where(a => a is not null)
            .Select(a => a!.Name ?? "unnamed")
            .ToList();
}
