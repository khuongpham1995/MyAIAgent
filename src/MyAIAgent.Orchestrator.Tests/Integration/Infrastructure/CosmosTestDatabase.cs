using Microsoft.EntityFrameworkCore;
using MyAIAgent.Orchestrator.Infrastructure.Data;

namespace MyAIAgent.Orchestrator.Tests.Integration.Infrastructure;

public sealed class CosmosTestDatabase : IAsyncDisposable
{
    private static readonly SemaphoreSlim EnsureCreatedLock = new(1, 1);
    private static bool isCreated;

    private readonly List<string> userIds = [];
    private readonly List<string> agentIds = [];

    private CosmosTestDatabase()
    {
    }

    public static async Task<CosmosTestDatabase> CreateAsync()
    {
        var database = new CosmosTestDatabase();
        await EnsureCreatedOnceAsync(database);
        return database;
    }

    public OrchestratorDbContext CreateContext()
    {
        var connectionString = CosmosTestEnvironment.ConnectionString
            ?? throw new InvalidOperationException(
                $"{CosmosTestEnvironment.ConnectionStringVariable} is not set; use [CosmosFact] so the test is skipped instead.");

        var options = new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseCosmos(connectionString, CosmosTestEnvironment.DatabaseName)
            .Options;

        return new OrchestratorDbContext(options);
    }

    /// <summary>A unique user id; everything created for it is deleted when the test ends.</summary>
    public string NewUserId()
    {
        var userId = $"test-user-{Guid.NewGuid():N}";
        userIds.Add(userId);
        return userId;
    }

    public string NewSessionId() => $"test-session-{Guid.NewGuid():N}";

    /// <summary>A unique agent id; the agent is deleted when the test ends.</summary>
    public string NewAgentId()
    {
        var agentId = $"test-agent-{Guid.NewGuid():N}";
        agentIds.Add(agentId);
        return agentId;
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = CreateContext();

        foreach (var userId in userIds)
        {
            var sessions = await context.ChatSessions.Where(session => session.UserId == userId).ToListAsync();
            context.ChatSessions.RemoveRange(sessions);
        }

        foreach (var agentId in agentIds)
        {
            var agents = await context.AgentDefinitions.Where(agent => agent.Id == agentId).ToListAsync();
            context.AgentDefinitions.RemoveRange(agents);
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureCreatedOnceAsync(CosmosTestDatabase database)
    {
        await EnsureCreatedLock.WaitAsync();
        try
        {
            if (isCreated)
                return;

            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync();
            isCreated = true;
        }
        finally
        {
            EnsureCreatedLock.Release();
        }
    }
}
