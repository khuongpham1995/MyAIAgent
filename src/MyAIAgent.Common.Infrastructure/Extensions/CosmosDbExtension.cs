using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Infrastructure.Data;

namespace MyAIAgent.Common.Infrastructure.Extensions;

public static class CosmosDbExtension
{
    public const string TestDatabaseName = "myaiagent-test";

    public static IServiceCollection AddCosmosDb<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
        where TContext : BaseDbContext
    {
        var connectionString = configuration.GetConnectionString("CosmosDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Connection string 'CosmosDb' is missing. Locally, set it with " +
                "'dotnet user-secrets set \"ConnectionStrings:CosmosDb\" \"<connection string>\"'.");

        var databaseName = configuration["CosmosDb:DatabaseName"]
            ?? throw new InvalidOperationException("Configuration key 'CosmosDb:DatabaseName' is missing");

        // Test and production databases share one account (and therefore one key), so a developer machine
        // must be unable to start against the production database. Exactly one name is allowed because
        // EnsureCreated would otherwise add a database with its own 400 RU/s and push the account past the free tier.
        if (isDevelopment && !string.Equals(databaseName, TestDatabaseName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"In Development 'CosmosDb:DatabaseName' must be '{TestDatabaseName}' (got '{databaseName}').");

        services.AddDbContext<TContext>(options => options.UseCosmos(connectionString, databaseName));

        return services;
    }

    // Creates the database and containers if missing. Deployed environments are provisioned with Bicep instead.
    public static async Task EnsureCosmosCreatedAsync<TContext>(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
        where TContext : BaseDbContext
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}
