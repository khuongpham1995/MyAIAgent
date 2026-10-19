using Azure.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Infrastructure.Data;

namespace MyAIAgent.Orchestrator.Tests.Unit.Infrastructure.Data;

public sealed class ChatSessionConcurrencyTests
{
    [Fact]
    public void Model_WhenSessionCanBeUpdatedByOverlappingRequests_ShouldRequireCurrentCosmosETag()
    {
        // Arrange
        using var context = new OrchestratorDbContext(new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseCosmos("https://localhost:9/", new AzureCliCredential(), "myaiagent-test")
            .Options);

        // Act
        var etag = context.Model.FindEntityType(typeof(ChatSession))!.GetETagProperty();

        // Assert
        etag.Should().NotBeNull("stale metadata and turn saves must not overwrite a newer conversation");
        etag!.IsConcurrencyToken.Should().BeTrue();
        etag.GetJsonPropertyName().Should().Be("_etag");
        etag.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
    }
}
