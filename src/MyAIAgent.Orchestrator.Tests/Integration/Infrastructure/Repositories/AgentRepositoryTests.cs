using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Infrastructure.Data.Repositories;

namespace MyAIAgent.Orchestrator.Tests.Integration.Infrastructure.Repositories;

public sealed class AgentRepositoryTests
{
    [CosmosFact]
    public async Task AddAndDelete_WhenCommitted_ShouldPersistChanges()
    {
        // Arrange
        await using var database = await CosmosTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new AgentRepository(context);
        var unitOfWork = new OrchestratorUnitOfWork(context);
        var agent = new AgentDefinition { Id = database.NewAgentId(), Name = "Test agent" };

        // Act
        await repository.AddAsync(agent, CancellationToken.None);

        // Assert
        await using var otherContext = database.CreateContext();
        (await otherContext.AgentDefinitions.AnyAsync(item => item.Id == agent.Id)).Should().BeFalse();

        // Act
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Assert
        (await otherContext.AgentDefinitions.AnyAsync(item => item.Id == agent.Id)).Should().BeTrue();

        // Act
        repository.Delete(agent);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Assert
        (await otherContext.AgentDefinitions.AnyAsync(item => item.Id == agent.Id)).Should().BeFalse();
    }
}
