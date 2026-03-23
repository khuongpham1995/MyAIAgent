using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using MyAIAgent.Common.Application.Extensions;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.McpServer.Domain.Entities;
using MyAIAgent.McpServer.Domain.Interfaces;
using MyAIAgent.McpServer.Infrastructure.ApiClients;

namespace MyAIAgent.McpServer.Infrastructure.Extensions;

public static class IoCExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddTransientAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddScopedAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddSingletonAsMatchingInterfaces(typeof(IoCExtension).Assembly);

        services
            .AddHttpClient<IApiClient<Customer>, ApiClient<Customer>>(client =>
            {
                var domain = configuration["MockApi:BaseUrl"]
                             ?? throw new InvalidOperationException(
                                 "Configuration key 'MockApi:BaseUrl' is missing");

                client.BaseAddress = new Uri(domain);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            }).AddHttpRetryPolicy();

        var kernelMemoryApiUrl = configuration["KernelMemory:BaseUrl"] ??
                                 throw new InvalidOperationException(
                                     "Configuration key 'KernelMemory:BaseUrl' is missing");
        services.AddSingleton<IKernelMemory>(_ => new MemoryWebClient(kernelMemoryApiUrl));
    }
}
