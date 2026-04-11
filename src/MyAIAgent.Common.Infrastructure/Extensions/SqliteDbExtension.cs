using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyAIAgent.Common.Infrastructure.Data;

namespace MyAIAgent.Common.Infrastructure.Extensions;

public static class SqliteDbExtension
{
    public static IServiceCollection AddSqliteDb<TContext>(
        this IServiceCollection services,
        string dataSource)
        where TContext : BaseDbContext
    {
        services.AddDbContext<TContext>(options => options.UseSqlite($"Data Source={dataSource}"));
        return services;
    }

    public static async Task EnsureDbCreatedAsync<TContext>(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
        where TContext : BaseDbContext
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}
