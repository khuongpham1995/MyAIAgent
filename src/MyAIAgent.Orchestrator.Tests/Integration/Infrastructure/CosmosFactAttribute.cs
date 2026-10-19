namespace MyAIAgent.Orchestrator.Tests.Integration.Infrastructure;

/// <summary>A fact that needs the Cosmos test database. It is reported as skipped, not passed, when no connection string is set.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CosmosFactAttribute : FactAttribute
{
    public CosmosFactAttribute()
    {
        if (CosmosTestEnvironment.ConnectionString is null)
            Skip = $"Cosmos test database not configured: set {CosmosTestEnvironment.ConnectionStringVariable}.";
    }
}
