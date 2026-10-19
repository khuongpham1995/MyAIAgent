using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MyAIAgent.KernelMemory.Infrastructure.Data;

namespace MyAIAgent.KernelMemory.Tests.Unit.Infrastructure;

// The free tier covers 1000 RU/s per account; dedicated container throughput would silently exceed it.
public sealed class KernelMemoryModelThroughputTests
{
    private static IModel DesignModel(KernelMemoryDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

    private static KernelMemoryDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<KernelMemoryDbContext>()
            .UseCosmos("AccountEndpoint=https://localhost:9/;AccountKey=eA==;", "myaiagent-test")
            .Options);

    [Fact]
    public void Model_WhenBuilt_ShouldShareFourHundredRequestUnitsAndNoDedicatedContainerThroughput()
    {
        using var context = CreateContext();
        var model = DesignModel(context);

        model.GetThroughput()!.Throughput.Should().Be(400);
        model.GetEntityTypes().Where(entityType => entityType.GetThroughput() is not null).Should().BeEmpty();
    }

    [Fact]
    public void Model_WhenBuilt_ShouldMapDocumentRecordsToTheirContainer()
    {
        using var context = CreateContext();

        DesignModel(context).GetEntityTypes().Select(entityType => entityType.GetContainer())
            .Should().Equal("DocumentRecords");
    }

    // infra/cosmos.bicep creates the container with this partition key path; a drift would break deployed environments.
    [Fact]
    public void Model_WhenBuilt_ShouldUseThePartitionKeyThatMatchesTheBicepContainer()
    {
        using var context = CreateContext();

        DesignModel(context).GetEntityTypes()
            .Select(entityType => string.Join("/", entityType.GetPartitionKeyPropertyNames()))
            .Should().Equal("IndexName");
    }
}
