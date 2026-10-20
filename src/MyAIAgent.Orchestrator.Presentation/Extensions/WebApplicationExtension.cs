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
        var database = configuration["CosmosDb:DatabaseName"] ?? "Unknown";
        var aiProvider = configuration["AiProvider:Type"] ?? "Unknown";

        logger.LogInformation(
            "Orchestrator configured for {Environment}: AI provider {AiProvider}, Cosmos database {Database}, authorization enabled: {AuthorizationEnabled}",
            environment.EnvironmentName, aiProvider, database, auth.Enabled);
    }
}
