using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MyAIAgent.Orchestrator.Infrastructure.Data;

namespace MyAIAgent.Orchestrator.Tests.Unit.Infrastructure.Data;

// The free tier covers 1000 RU/s per account; dedicated container throughput would silently exceed it.
public sealed class OrchestratorModelThroughputTests
{
    private static OrchestratorDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseCosmos("AccountEndpoint=https://localhost:9/;AccountKey=eA==;", "myaiagent-test")
            .Options);

    // Throughput settings are design-time annotations; the runtime model strips them.
    private static IModel DesignModel(OrchestratorDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void Model_WhenBuilt_ShouldShareFourHundredRequestUnitsAcrossTheDatabase()
    {
        using var context = CreateContext();

        var throughput = DesignModel(context).GetThroughput();

        throughput.Should().NotBeNull();
        throughput!.Throughput.Should().Be(400);
        throughput.AutoscaleMaxThroughput.Should().BeNull();
    }

    [Fact]
    public void Model_WhenBuilt_ShouldNotGiveAnyContainerDedicatedThroughput()
    {
        using var context = CreateContext();

        var dedicated = DesignModel(context).GetEntityTypes()
            .Where(entityType => entityType.GetThroughput() is not null)
            .Select(entityType => entityType.DisplayName());

        dedicated.Should().BeEmpty();
    }

    [Fact]
    public void Model_WhenBuilt_ShouldMapSessionsAndAgentsToTheirOwnContainers()
    {
        using var context = CreateContext();

        var containers = DesignModel(context).GetEntityTypes()
            .Where(entityType => !entityType.IsOwned())
            .Select(entityType => entityType.GetContainer())
            .Distinct()
            .Order();

        containers.Should().Equal("AgentDefinitions", "ChatSessions");
    }

    // infra/cosmos.bicep creates the containers with these partition key paths; a drift would break deployed environments.
    [Fact]
    public void Model_WhenBuilt_ShouldUsePartitionKeysThatMatchTheBicepContainers()
    {
        using var context = CreateContext();

        var keys = DesignModel(context).GetEntityTypes()
            .Where(entityType => !entityType.IsOwned())
            .ToDictionary(
                entityType => entityType.GetContainer()!,
                entityType => string.Join("/", entityType.GetPartitionKeyPropertyNames()));

        keys.Should().Equal(new Dictionary<string, string>
        {
            ["ChatSessions"] = "UserId",
            ["AgentDefinitions"] = "Id",
        });
    }
}
