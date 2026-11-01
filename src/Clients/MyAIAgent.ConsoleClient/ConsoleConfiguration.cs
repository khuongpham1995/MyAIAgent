/**
 * MyAIAgent | Console Client
 *
 * Loads local client settings in override order without depending on the terminal's working directory.
 */

using Microsoft.Extensions.Configuration;

namespace MyAIAgent.ConsoleClient;

internal static class ConsoleConfiguration
{
    public static ClientOptions Load(string[] args, string basePath)
    {
        if (args is not ([] or ["--gateway", _]))
            throw new ArgumentException("Usage: --gateway <local Gateway URL>, or --help.");

        using var configuration = new ConfigurationManager();
        configuration
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(environment))
            configuration.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);

        configuration
            .AddEnvironmentVariables()
            .AddCommandLine(args, new Dictionary<string, string> { ["--gateway"] = "Gateway:BaseUrl" });
        return ClientOptions.FromConfiguration(configuration);
    }
}
