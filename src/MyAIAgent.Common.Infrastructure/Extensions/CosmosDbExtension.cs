using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Infrastructure.Data;

namespace MyAIAgent.Common.Infrastructure.Extensions;

public static class CosmosDbExtension
{
    public static IServiceCollection AddCosmosDb<TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TContext : BaseDbContext
    {
        var connectionString = configuration.GetConnectionString("CosmosDb")
            ?? throw new InvalidOperationException("Connection string 'CosmosDb' is missing");

        var databaseName = configuration["CosmosDb:DatabaseName"]
            ?? throw new InvalidOperationException("Configuration key 'CosmosDb:DatabaseName' is missing");

        services.AddDbContext<TContext>(options =>
            options.UseCosmos(connectionString, databaseName));

        return services;
    }

    public static async Task EnsureCosmosDbCreatedAsync<TContext>(this IServiceProvider services)
        where TContext : BaseDbContext
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
