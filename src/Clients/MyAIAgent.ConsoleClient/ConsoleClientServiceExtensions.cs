/**
 * MyAIAgent | Console Client
 *
 * Shares the real console transport registration with tests while per-operation deadlines own cancellation.
 */

using Microsoft.Extensions.DependencyInjection;

namespace MyAIAgent.ConsoleClient;

internal static class ConsoleClientServiceExtensions
{
    public static IHttpClientBuilder AddConsoleClient(this IServiceCollection services, ClientOptions options)
    {
        services.AddSingleton(options);
        services.AddTransient<GatewayResponseHandler>();
        return services.AddHttpClient<GatewayClient>(client =>
        {
            client.BaseAddress = options.Gateway;
            // JSON chat may spend its full deadline waiting for headers; linked tokens bound every call.
            client.Timeout = Timeout.InfiniteTimeSpan;
        }).AddHttpMessageHandler<GatewayResponseHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                UseProxy = false,
            });
    }
}
