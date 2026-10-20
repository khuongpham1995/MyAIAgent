/**
 * MyAIAgent | Orchestrator · Presentation
 *
 * Runs Orchestrator and logs nonsecret startup diagnostics for its configured AI provider.
 */

using Microsoft.Extensions.Options;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Orchestrator.Presentation.Extensions;

internal static class WebApplicationExtension
{
    internal static async Task RunOrchestratorAsync(this WebApplication app, IConfiguration configuration)
    {
        var auth = app.Services.GetRequiredService<IOptions<ServiceAuthorizationModel>>().Value;
        LogStartupDiagnostics(app.Logger, app.Environment, configuration, auth);

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
        IConfiguration configuration,
        ServiceAuthorizationModel auth)
    {
        var env = environment.EnvironmentName;
        var database = configuration["CosmosDb:DatabaseName"] ?? "Unknown";
        var aiProvider = configuration["AiProvider:Type"] ?? "Unknown";
        var authStatus = auth.Enabled ? "Enabled" : "Disabled";

        logger.LogInformation("*******************************************************************************");
        logger.LogInformation("* Environment         : {Environment}", env);
        logger.LogInformation("* Cosmos DB database  : {Database}", database);
        logger.LogInformation("* AI provider         : {AiProvider}", aiProvider);
        logger.LogInformation("* Auth                : {AuthStatus}", authStatus);
        logger.LogInformation("* Endpoints           : POST /chat");
        logger.LogInformation("*                       POST /chat/stream");
        logger.LogInformation("*                       GET  /conversations");
        logger.LogInformation("*                       GET  /conversations/{{id}}");
        logger.LogInformation("*                       PUT  /conversations/{{id}}/title");
        logger.LogInformation("*                       DELETE /conversations/{{id}}");
        logger.LogInformation("*                       GET  /agents");
        logger.LogInformation("*                       POST /agents");
        logger.LogInformation("*                       DELETE /agents/{{id}}");
        logger.LogInformation("*******************************************************************************");
    }
}
