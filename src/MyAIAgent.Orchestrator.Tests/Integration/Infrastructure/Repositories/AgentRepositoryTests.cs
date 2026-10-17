using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Infrastructure.Data.Repositories;

namespace MyAIAgent.Orchestrator.Tests.Integration.Infrastructure.Repositories;

public sealed class AgentRepositoryTests
{
    [Fact]
    public async Task AddAndDelete_WhenCommitted_ShouldPersistChanges()
    {
        using var factory = OrchestratorDbContextFactory.Create();
        await factory.Context.Database.EnsureCreatedAsync();
        var repository = new AgentRepository(factory.Context);
        var unitOfWork = new OrchestratorUnitOfWork(factory.Context);
        var agent = new AgentDefinition { Id = "agent-001", Name = "Test agent" };

        await repository.AddAsync(agent, CancellationToken.None);

        using var otherContext = factory.CreateAdditionalContext();
        (await otherContext.AgentDefinitions.AnyAsync(item => item.Id == agent.Id)).Should().BeFalse();

        await unitOfWork.SaveChangesAsync(CancellationToken.None);
        (await otherContext.AgentDefinitions.AnyAsync(item => item.Id == agent.Id)).Should().BeTrue();

        repository.Delete(agent);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
        (await otherContext.AgentDefinitions.AnyAsync(item => item.Id == agent.Id)).Should().BeFalse();
    }
}
