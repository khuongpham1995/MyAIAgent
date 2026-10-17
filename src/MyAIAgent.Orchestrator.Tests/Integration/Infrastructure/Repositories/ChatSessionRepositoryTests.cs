using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Infrastructure.Data.Repositories;

namespace MyAIAgent.Orchestrator.Tests.Integration.Infrastructure.Repositories;

public sealed class ChatSessionRepositoryTests
{
    [Fact]
    public async Task AddAsync_WhenSessionIsNew_ShouldPersistSession()
    {
        // Arrange
        using var factory = OrchestratorDbContextFactory.Create();
        await factory.Context.Database.EnsureCreatedAsync();

        var repository = new ChatSessionRepository(factory.Context);
        var session = new ChatSession
        {
            SessionId = "session-add-001",
            UserId = "user-001",
            Title = "Test Session",
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await repository.AddAsync(session, CancellationToken.None);
        await new OrchestratorUnitOfWork(factory.Context).SaveChangesAsync(CancellationToken.None);

        // Assert
        var persisted = await repository.FindBySessionIdAsync("session-add-001", CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.SessionId.Should().Be("session-add-001");
        persisted.UserId.Should().Be("user-001");
        persisted.Title.Should().Be("Test Session");
    }

    [Fact]
    public async Task FindBySessionIdAsync_WhenSessionExists_ShouldReturnSession()
    {
        // Arrange
        using var factory = OrchestratorDbContextFactory.Create();
        await factory.Context.Database.EnsureCreatedAsync();

        var repository = new ChatSessionRepository(factory.Context);
        var session = new ChatSession
        {
            SessionId = "session-find-001",
            UserId = "user-002",
            Title = "Find Me",
            CreatedAt = DateTimeOffset.UtcNow,
            Messages =
            [
                new ChatSessionMessage { Role = "user", Content = "Hello", CreatedAt = DateTimeOffset.UtcNow }
            ]
        };
        await repository.AddAsync(session, CancellationToken.None);
        await new OrchestratorUnitOfWork(factory.Context).SaveChangesAsync(CancellationToken.None);

        // Act
        var result = await repository.FindBySessionIdAsync("session-find-001", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.SessionId.Should().Be("session-find-001");
        result.Messages.Should().ContainSingle(m => m.Role == "user" && m.Content == "Hello");
    }

    [Fact]
    public async Task FindBySessionIdAsync_WhenSessionNotFound_ShouldReturnNull()
    {
        // Arrange
        using var factory = OrchestratorDbContextFactory.Create();
        await factory.Context.Database.EnsureCreatedAsync();

        var repository = new ChatSessionRepository(factory.Context);

        // Act
        var result = await repository.FindBySessionIdAsync("nonexistent-session", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenSessionIsModified_ShouldPersistChanges()
    {
        // Arrange
        using var factory = OrchestratorDbContextFactory.Create();
        await factory.Context.Database.EnsureCreatedAsync();

        var repository = new ChatSessionRepository(factory.Context);
        var session = new ChatSession
        {
            SessionId = "session-update-001",
            UserId = "user-003",
            Title = "Original Title",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repository.AddAsync(session, CancellationToken.None);
        var unitOfWork = new OrchestratorUnitOfWork(factory.Context);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Act
        session.Title = "Updated Title";
        session.Messages.Add(new ChatSessionMessage
        {
            Role = "assistant",
            Content = "Updated response",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Assert
        var updated = await repository.FindBySessionIdAsync("session-update-001", CancellationToken.None);
        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Updated Title");
        updated.Messages.Should().ContainSingle(m => m.Role == "assistant" && m.Content == "Updated response");
    }

    [Fact]
    public async Task AddAsync_BeforeUnitOfWorkCommit_ShouldNotPersistSession()
    {
        using var factory = OrchestratorDbContextFactory.Create();
        await factory.Context.Database.EnsureCreatedAsync();
        var repository = new ChatSessionRepository(factory.Context);
        var session = new ChatSession { SessionId = "session-pending", UserId = "user-001" };

        await repository.AddAsync(session, CancellationToken.None);

        using var otherContext = factory.CreateAdditionalContext();
        var beforeCommit = await otherContext.ChatSessions
            .AnyAsync(item => item.SessionId == session.SessionId);
        beforeCommit.Should().BeFalse();

        await new OrchestratorUnitOfWork(factory.Context).SaveChangesAsync(CancellationToken.None);

        var afterCommit = await otherContext.ChatSessions
            .AnyAsync(item => item.SessionId == session.SessionId);
        afterCommit.Should().BeTrue();
    }
}
