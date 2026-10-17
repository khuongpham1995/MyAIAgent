using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyAIAgent.Orchestrator.Infrastructure.Data;

namespace MyAIAgent.Orchestrator.Tests.Integration.Infrastructure;

public sealed class OrchestratorDbContextFactory : IDisposable
{
    private readonly SqliteConnection connection;

    public OrchestratorDbContext Context { get; }

    private OrchestratorDbContextFactory(SqliteConnection connection, OrchestratorDbContext context)
    {
        this.connection = connection;
        Context = context;
    }

    public static OrchestratorDbContextFactory Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new OrchestratorDbContext(options);
        return new OrchestratorDbContextFactory(connection, context);
    }

    public OrchestratorDbContext CreateAdditionalContext()
    {
        var options = new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseSqlite(connection)
            .Options;
        return new OrchestratorDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        connection.Dispose();
    }
}
