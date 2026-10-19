using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Application.Extensions;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Gateway.Domain.Constants;
using MyAIAgent.Gateway.Application.Common.Interfaces;
using MyAIAgent.Gateway.Infrastructure.Services;

namespace MyAIAgent.Gateway.Infrastructure;

public static class IoCExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        services
            .AddTransientAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddScopedAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddSingletonAsMatchingInterfaces(typeof(IoCExtension).Assembly);

        var orchestratorBaseUrl = configuration[GatewayConstant.Config.OrchestratorBaseUrl]
            ?? throw new InvalidOperationException($"Configuration key '{GatewayConstant.Config.OrchestratorBaseUrl}' is missing");

        var kernelMemoryBaseUrl = configuration[GatewayConstant.Config.KernelMemoryBaseUrl]
            ?? throw new InvalidOperationException($"Configuration key '{GatewayConstant.Config.KernelMemoryBaseUrl}' is missing");

        var mcpServerBaseUrl = configuration[GatewayConstant.Config.McpServerBaseUrl]
            ?? throw new InvalidOperationException($"Configuration key '{GatewayConstant.Config.McpServerBaseUrl}' is missing");

        // The Orchestrator trusts the user id it is given, so in production it only accepts callers holding this key.
        var orchestratorAccessKey = configuration[GatewayConstant.Config.OrchestratorAccessKey];
        if (!isDevelopment && string.IsNullOrWhiteSpace(orchestratorAccessKey))
            throw new InvalidOperationException($"{GatewayConstant.Config.OrchestratorAccessKey} is required in production.");

        var kernelMemoryAccessKey = configuration["KernelMemory:AccessKey"];
        if (!isDevelopment && string.IsNullOrWhiteSpace(kernelMemoryAccessKey))
            throw new InvalidOperationException("KernelMemory:AccessKey is required in production.");

        services.AddHttpClient<IOrchestratorClient, OrchestratorClient>(client =>
        {
            client.BaseAddress = new Uri(orchestratorBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            AddServiceKey(client, orchestratorAccessKey);
        }).AddHttpRetryPolicy();

        // No retry handler: replaying a chat request after part of the stream was delivered would duplicate output.
        services.AddHttpClient<IOrchestratorStreamClient, OrchestratorStreamClient>(client =>
        {
            client.BaseAddress = new Uri(orchestratorBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
            AddServiceKey(client, orchestratorAccessKey);
        });

        services.AddHttpClient<IKernelMemoryClient, KernelMemoryClient>(client =>
        {
            client.BaseAddress = new Uri(kernelMemoryBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
            if (!string.IsNullOrWhiteSpace(kernelMemoryAccessKey))
                client.DefaultRequestHeaders.Add("Authorization", kernelMemoryAccessKey);
        }).AddHttpRetryPolicy();

        services.AddHttpClient<IMcpServerClient, McpServerClient>(client =>
        {
            client.BaseAddress = new Uri(mcpServerBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        }).AddHttpRetryPolicy();
    }

    private static void AddServiceKey(HttpClient client, string? accessKey)
    {
        if (!string.IsNullOrWhiteSpace(accessKey))
            client.DefaultRequestHeaders.Add(GatewayConstant.Config.ServiceAuthorizationHeader, accessKey);
    }
}
