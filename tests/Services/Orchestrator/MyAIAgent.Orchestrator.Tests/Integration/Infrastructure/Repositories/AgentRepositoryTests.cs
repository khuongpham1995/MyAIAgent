/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Exercises committing agent creation and deletion against the explicitly configured live Cosmos test database.
 */

using FluentAssertions;
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
        var otherRepository = new AgentRepository(otherContext);
        (await otherRepository.FindByIdAsync(agent.Id, CancellationToken.None)).Should().BeNull();

        // Act
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Assert
        (await otherRepository.FindByIdAsync(agent.Id, CancellationToken.None)).Should().NotBeNull();

        // Act
        repository.Delete(agent);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Assert
        (await otherRepository.FindByIdAsync(agent.Id, CancellationToken.None)).Should().BeNull();
    }
}
