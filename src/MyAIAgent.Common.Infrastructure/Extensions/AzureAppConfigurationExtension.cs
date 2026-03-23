using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MyAIAgent.Common.Infrastructure.Extensions;

public static class AzureAppConfigurationExtension
{
    public static IHostApplicationBuilder AddAzureAppConfiguration(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration["ConnectionStrings:AppConfig"];

        if (string.IsNullOrEmpty(connectionString))
            return builder;

        builder.Configuration.AddAzureAppConfiguration(options =>
            options.Connect(connectionString)
                   .ConfigureRefresh(refresh =>
                       refresh.Register("Sentinel", refreshAll: true)
                              .SetRefreshInterval(TimeSpan.FromSeconds(30))));

        builder.Services.AddAzureAppConfiguration();

        return builder;
    }
}
