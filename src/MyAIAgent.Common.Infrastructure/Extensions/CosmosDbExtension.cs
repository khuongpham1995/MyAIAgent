using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Infrastructure.Data;

namespace MyAIAgent.Common.Infrastructure.Extensions;

public static class CosmosDbExtension
{
    public const string TestDatabaseName = "myaiagent-test";

    public static IServiceCollection AddCosmosDb<TContext>(
        this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
        where TContext : BaseDbContext
    {
        var endpoint = AzureCredentialFactory.RequireHttpsEndpoint(configuration["CosmosDb:Endpoint"], "CosmosDb:Endpoint");
        var databaseName = configuration["CosmosDb:DatabaseName"];
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("CosmosDb:DatabaseName is required.");

        if (isDevelopment && !string.Equals(databaseName, TestDatabaseName, StringComparison.Ordinal))
            throw new InvalidOperationException($"In Development 'CosmosDb:DatabaseName' must be '{TestDatabaseName}'.");

        var credential = AzureCredentialFactory.Create(configuration, isDevelopment);
        services.AddDbContext<TContext>(options => options.UseCosmos(endpoint.AbsoluteUri, credential, databaseName));
        return services;
    }
}
