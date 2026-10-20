/**
 * MyAIAgent | Common · Infrastructure
 *
 * Loads service-prefixed AAC settings with Azure identity while preserving local configuration overrides.
 */

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;

namespace MyAIAgent.Common.Infrastructure.Extensions;

public static class AzureAppConfigurationExtension
{
    public static IHostApplicationBuilder AddAzureAppConfiguration(this IHostApplicationBuilder builder)
    {
        var endpointValue = builder.Configuration["AppConfiguration:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpointValue) && builder.Environment.IsDevelopment())
            return builder;

        var endpoint = AzureCredentialFactory.RequireHttpsEndpoint(endpointValue, "AppConfiguration:Endpoint");
        var credential = AzureCredentialFactory.Create(builder.Configuration, builder.Environment.IsDevelopment());
        var prefix = builder.Configuration["AppConfiguration:KeyPrefix"];
        if (string.IsNullOrWhiteSpace(prefix) || !prefix.EndsWith(':') || prefix.IndexOfAny(['*', ',', '\\']) >= 0)
            throw new InvalidOperationException("AppConfiguration:KeyPrefix must be a service prefix ending with ':' without filter wildcards.");
        var label = builder.Configuration["AppConfiguration:Label"] ?? builder.Environment.EnvironmentName;

        var remote = new ConfigurationBuilder().AddAzureAppConfiguration(options =>
            options.Connect(endpoint, credential)
                .Select(prefix + "*", label)
                .TrimKeyPrefix(prefix)
                .ConfigureRefresh(refresh => refresh.Register(prefix + "Sentinel", label, refreshAll: true)
                    .SetRefreshInterval(TimeSpan.FromSeconds(30))));

        InsertBeforeLocalOverrides(builder.Configuration, remote.Sources.Single());
        builder.Services.AddAzureAppConfiguration();
        return builder;
    }

    internal static void InsertBeforeLocalOverrides(IConfigurationManager configuration, IConfigurationSource remote)
    {
        // Deployment secrets, developer secrets, environment variables and command-line values retain priority over AAC.
        var insertionIndex = configuration.Sources.Select((source, index) => (source, index))
            .Where(item => item.source is JsonConfigurationSource json
                && json.Path is not null && Path.GetFileName(json.Path).StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index + 1)
            .DefaultIfEmpty(0)
            .Max();
        configuration.Sources.Insert(insertionIndex, remote);
    }

    public static IApplicationBuilder UseAzureAppConfigurationWhenConfigured(
        this IApplicationBuilder app, IConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration["AppConfiguration:Endpoint"]) ? app : app.UseAzureAppConfiguration();
}
