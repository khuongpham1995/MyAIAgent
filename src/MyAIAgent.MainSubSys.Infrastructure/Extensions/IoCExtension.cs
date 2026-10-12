using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Application.Extensions;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.MainSubSys.Domain.Constants;
using MyAIAgent.MainSubSys.Domain.Interfaces;
using MyAIAgent.MainSubSys.Infrastructure.Clients;

namespace MyAIAgent.MainSubSys.Infrastructure.Extensions;

public static class IoCExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        services
            .AddTransientAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddScopedAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddSingletonAsMatchingInterfaces(typeof(IoCExtension).Assembly);

        var orchestratorBaseUrl = configuration[MainSubSysConstant.Config.OrchestratorBaseUrl]
            ?? throw new InvalidOperationException($"Configuration key '{MainSubSysConstant.Config.OrchestratorBaseUrl}' is missing");

        var kernelMemoryBaseUrl = configuration[MainSubSysConstant.Config.KernelMemoryBaseUrl]
            ?? throw new InvalidOperationException($"Configuration key '{MainSubSysConstant.Config.KernelMemoryBaseUrl}' is missing");

        var mcpServerBaseUrl = configuration[MainSubSysConstant.Config.McpServerBaseUrl]
            ?? throw new InvalidOperationException($"Configuration key '{MainSubSysConstant.Config.McpServerBaseUrl}' is missing");

        var kernelMemoryAccessKey = configuration["KernelMemory:AccessKey"];
        if (!isDevelopment && string.IsNullOrWhiteSpace(kernelMemoryAccessKey))
            throw new InvalidOperationException("KernelMemory:AccessKey is required in production.");

        services.AddHttpClient<IOrchestratorClient, OrchestratorClient>(client =>
        {
            client.BaseAddress = new Uri(orchestratorBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddHttpRetryPolicy();

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
}
