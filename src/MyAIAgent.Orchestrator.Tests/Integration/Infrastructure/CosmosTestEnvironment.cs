/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Resolves and validates the explicit endpoint used by live Cosmos integration tests.
 */

namespace MyAIAgent.Orchestrator.Tests.Integration.Infrastructure;

public static class CosmosTestEnvironment
{
    public const string EndpointVariable = "COSMOS_TEST_ENDPOINT";
    public const string DatabaseName = "myaiagent-test";

    public static string? Endpoint =>
        Environment.GetEnvironmentVariable(EndpointVariable) is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;
}
