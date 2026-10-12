using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Application.Extensions;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Common.Infrastructure.Filters;

using MyAIAgent.McpServer.Domain.Interfaces;
using MyAIAgent.McpServer.Infrastructure.Services;

namespace MyAIAgent.McpServer.Infrastructure.Extensions;

public static class IoCExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddTransientAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddScopedAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddSingletonAsMatchingInterfaces(typeof(IoCExtension).Assembly);

        services.Configure<ServiceAuthorizationModel>(
            configuration.GetSection("ServiceAuthorization"));

        services.AddSingleton<HttpAuthEndpointFilter>();

        services.AddHttpClient<ISearchProvider, GoogleSearchClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        }).AddHttpRetryPolicy();

        services.AddHttpClient<IPageFetcher, PageFetcher>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MyAIAgent");
            client.Timeout = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectCallback = PublicPageAddressPolicy.ConnectAsync
        }).AddHttpRetryPolicy();
    }
}
