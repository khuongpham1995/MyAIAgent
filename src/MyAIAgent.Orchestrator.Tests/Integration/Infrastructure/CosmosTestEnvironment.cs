namespace MyAIAgent.Orchestrator.Tests.Integration.Infrastructure;

public static class CosmosTestEnvironment
{
    public const string ConnectionStringVariable = "COSMOS_TEST_CONNECTION_STRING";

    // Tests only ever touch the test database; the production database shares the account but is never named here.
    public const string DatabaseName = "myaiagent-test";

    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable) is { Length: > 0 } value ? value : null;
}
