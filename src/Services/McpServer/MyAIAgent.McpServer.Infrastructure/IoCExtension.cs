/**
 * MyAIAgent | McpServer · Infrastructure
 *
 * Registers service authorization and the fixed-destination search client without automatic retries.
 */

using MyAIAgent.Common.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Application.Extensions;
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

        services.AddHttpClient<ISearchProvider, TavilySearchClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectCallback = (context, cancellationToken) => new OutboundConnectionPolicy()
                .ConnectAsync(context.DnsEndPoint, OutboundAddressPolicy.IsPublicAddress, cancellationToken)
        }).AddHttpMessageHandler(() => new OutboundRequestHandler(uri => uri == TavilySearchClient.Endpoint));

        services.AddHttpClient<IWebPageReader, TavilyWebPageClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectCallback = (context, cancellationToken) => new OutboundConnectionPolicy()
                .ConnectAsync(context.DnsEndPoint, OutboundAddressPolicy.IsPublicAddress, cancellationToken)
        }).AddHttpMessageHandler(() => new OutboundRequestHandler(uri => uri == TavilyWebPageClient.Endpoint));
    }
}
