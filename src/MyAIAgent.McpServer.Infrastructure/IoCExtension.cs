/**
 * MyAIAgent | McpServer · Infrastructure
 *
 * Registers search and page-fetch clients with timeouts, retry policies, and public-address connection checks.
 */

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Application.Extensions;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Common.Infrastructure.Extensions;
using MyAIAgent.Common.Infrastructure.Filters;
using MyAIAgent.Common.Infrastructure.Http;
using MyAIAgent.McpServer.Application.Common.Interfaces;
using MyAIAgent.McpServer.Infrastructure.Services;

namespace MyAIAgent.McpServer.Infrastructure;

public static class IoCExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        if (!isDevelopment && !configuration.GetValue<bool>("ServiceAuthorization:Enabled"))
            throw new InvalidOperationException("ServiceAuthorization:Enabled must be true outside Development.");

        services
            .AddTransientAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddScopedAsMatchingInterfaces(typeof(IoCExtension).Assembly)
            .AddSingletonAsMatchingInterfaces(typeof(IoCExtension).Assembly);

        services.AddOptions<ServiceAuthorizationModel>()
            .Bind(configuration.GetSection("ServiceAuthorization"))
            .Validate(auth => !auth.Enabled
                || (!string.IsNullOrWhiteSpace(auth.AccessKey1) && !string.IsNullOrWhiteSpace(auth.AccessKey2)),
                "ServiceAuthorization requires both active and rotation keys when enabled.")
            .Validate(auth => !auth.Enabled || auth.HttpHeaderName == "Authorization",
                "MCP ServiceAuthorization:HttpHeaderName must be Authorization.")
            .ValidateOnStart();

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
            ConnectCallback = (context, cancellationToken) => new OutboundConnectionPolicy()
                .ConnectAsync(context.DnsEndPoint, OutboundAddressPolicy.IsPublicAddress, cancellationToken)
        }).AddHttpMessageHandler(() => new OutboundRequestHandler(PublicPageAddressPolicy.IsAllowedUri, rejectRedirects: false))
          .AddHttpRetryPolicy(excludeBlockedDestinations: true);
    }
}
