/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Marks live Cosmos tests as skipped when the required test endpoint is not configured.
 */

namespace MyAIAgent.Orchestrator.Tests.Integration.Infrastructure;

/// <summary>A fact that needs the Cosmos test database. It is reported as skipped, not passed, when no test endpoint is set.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CosmosFactAttribute : FactAttribute
{
    public CosmosFactAttribute()
    {
        if (CosmosTestEnvironment.Endpoint is null)
            Skip = $"Cosmos test database not configured: set {CosmosTestEnvironment.EndpointVariable}.";
    }
}
