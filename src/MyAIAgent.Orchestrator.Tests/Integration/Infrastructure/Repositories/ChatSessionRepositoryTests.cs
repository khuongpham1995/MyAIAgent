using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MyAIAgent.Orchestrator.Application.Common.Exceptions;
using MyAIAgent.Orchestrator.Domain.Entities;
using MyAIAgent.Orchestrator.Infrastructure.Data.Repositories;

namespace MyAIAgent.Orchestrator.Tests.Integration.Infrastructure.Repositories;

public sealed class ChatSessionRepositoryTests
{
    [CosmosFact]
    public async Task SaveChangesAsync_WhenMetadataSaveIsStale_ShouldPreserveConcurrentTurn()
    {
        await using var database = await CosmosTestDatabase.CreateAsync();
        await using var metadataContext = database.CreateContext();
        var repository = new ChatSessionRepository(metadataContext);
        var now = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var session = new ChatSession
        {
            SessionId = database.NewSessionId(),
            UserId = database.NewUserId(),
            Title = "Fallback title",
            Messages =
            [
                new ChatSessionMessage { Role = "user", Content = "First question", CreatedAt = now },
                new ChatSessionMessage { Role = "assistant", Content = "First reply", CreatedAt = now.AddTicks(1) }
            ]
        };
        await repository.AddAsync(session, CancellationToken.None);
        var metadataUnitOfWork = new OrchestratorUnitOfWork(metadataContext);
        await metadataUnitOfWork.SaveChangesAsync(CancellationToken.None);

        // A second request commits while the first is awaiting its title or summary provider.
        await using var turnContext = database.CreateContext();
        var concurrent = (await new ChatSessionRepository(turnContext)
            .FindForUserAsync(session.SessionId, session.UserId, CancellationToken.None))!;
        concurrent.Messages.Add(new ChatSessionMessage { Role = "user", Content = "Second question", CreatedAt = now.AddSeconds(1) });
        concurrent.Messages.Add(new ChatSessionMessage { Role = "assistant", Content = "Second reply", CreatedAt = now.AddSeconds(1).AddTicks(1) });
        await new OrchestratorUnitOfWork(turnContext).SaveChangesAsync(CancellationToken.None);
        session.Title = "Delayed title";
        session.Summary = "Delayed summary";
        session.SummarizedMessageCount = 2;

        var save = () => metadataUnitOfWork.SaveChangesAsync(CancellationToken.None);

        var failure = await save.Should().ThrowAsync<PersistenceException>();
        failure.Which.InnerException.Should().BeOfType<DbUpdateConcurrencyException>();
        await using var verificationContext = database.CreateContext();
        var persisted = (await new ChatSessionRepository(verificationContext)
            .FindForUserAsync(session.SessionId, session.UserId, CancellationToken.None))!;
        persisted.Messages.Select(message => message.Content).Should()
            .BeEquivalentTo("First question", "First reply", "Second question", "Second reply");
        persisted.Title.Should().Be("Fallback title");
        persisted.Summary.Should().BeNull();
        persisted.SummarizedMessageCount.Should().Be(0);
    }

    [CosmosFact]
    public async Task AddAsync_WhenSessionIsNew_ShouldPersistSession()
    {
        await using var database = await CosmosTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new ChatSessionRepository(context);
        var session = new ChatSession
        {
            SessionId = database.NewSessionId(),
            UserId = database.NewUserId(),
            Title = "Test Session",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(session, CancellationToken.None);
        await new OrchestratorUnitOfWork(context).SaveChangesAsync(CancellationToken.None);

        var persisted = await repository.FindForUserAsync(session.SessionId, session.UserId, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.SessionId.Should().Be(session.SessionId);
        persisted.UserId.Should().Be(session.UserId);
        persisted.Title.Should().Be("Test Session");
    }

    [CosmosFact]
    public async Task FindForUserAsync_WhenSessionExists_ShouldReturnSessionWithMessages()
    {
        await using var database = await CosmosTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new ChatSessionRepository(context);
        var session = new ChatSession
        {
            SessionId = database.NewSessionId(),
            UserId = database.NewUserId(),
            Title = "Find Me",
            CreatedAt = DateTimeOffset.UtcNow,
            Messages = [new ChatSessionMessage { Role = "user", Content = "Hello", CreatedAt = DateTimeOffset.UtcNow }]
        };
        await repository.AddAsync(session, CancellationToken.None);
        await new OrchestratorUnitOfWork(context).SaveChangesAsync(CancellationToken.None);

        await using var otherContext = database.CreateContext();
        var result = await new ChatSessionRepository(otherContext)
            .FindForUserAsync(session.SessionId, session.UserId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Messages.Should().ContainSingle(m => m.Role == "user" && m.Content == "Hello");
    }

    [CosmosFact]
    public async Task FindForUserAsync_WhenSessionNotFound_ShouldReturnNull()
    {
        await using var database = await CosmosTestDatabase.CreateAsync();
        await using var context = database.CreateContext();

        var result = await new ChatSessionRepository(context)
            .FindForUserAsync(database.NewSessionId(), database.NewUserId(), CancellationToken.None);

        result.Should().BeNull();
    }

    [CosmosFact]
    public async Task FindForUserAsync_WhenSessionBelongsToAnotherUser_ShouldReturnNull()
    {
        await using var database = await CosmosTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new ChatSessionRepository(context);
        var session = new ChatSession { SessionId = database.NewSessionId(), UserId = database.NewUserId() };
        await repository.AddAsync(session, CancellationToken.None);
        await new OrchestratorUnitOfWork(context).SaveChangesAsync(CancellationToken.None);

        await using var otherContext = database.CreateContext();
        var result = await new ChatSessionRepository(otherContext)
            .FindForUserAsync(session.SessionId, database.NewUserId(), CancellationToken.None);

        result.Should().BeNull();
    }

    [CosmosFact]
    public async Task UpdateAsync_WhenSessionIsModified_ShouldPersistChanges()
    {
        await using var database = await CosmosTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new ChatSessionRepository(context);
        var session = new ChatSession
        {
            SessionId = database.NewSessionId(),
            UserId = database.NewUserId(),
            Title = "Original Title",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repository.AddAsync(session, CancellationToken.None);
        var unitOfWork = new OrchestratorUnitOfWork(context);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        session.Title = "Updated Title";
        session.Messages.Add(new ChatSessionMessage
        {
            Role = "assistant",
            Content = "Updated response",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        await using var otherContext = database.CreateContext();
        var updated = await new ChatSessionRepository(otherContext)
            .FindForUserAsync(session.SessionId, session.UserId, CancellationToken.None);
        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Updated Title");
        updated.Messages.Should().ContainSingle(m => m.Role == "assistant" && m.Content == "Updated response");
    }

    [CosmosFact]
    public async Task AddAsync_BeforeUnitOfWorkCommit_ShouldNotPersistSession()
    {
        await using var database = await CosmosTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new ChatSessionRepository(context);
        var session = new ChatSession { SessionId = database.NewSessionId(), UserId = database.NewUserId() };

        await repository.AddAsync(session, CancellationToken.None);

        await using var otherContext = database.CreateContext();
        (await otherContext.ChatSessions.AnyAsync(item => item.SessionId == session.SessionId)).Should().BeFalse();

        await new OrchestratorUnitOfWork(context).SaveChangesAsync(CancellationToken.None);

        (await otherContext.ChatSessions.AnyAsync(item => item.SessionId == session.SessionId)).Should().BeTrue();
    }

    [CosmosFact]
    public async Task ListSummariesByUserAsync_WhenUserHasSessions_ShouldReturnOwnSessionsNewestFirst()
    {
        await using var database = await CosmosTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new ChatSessionRepository(context);
        var userId = database.NewUserId();
        var now = DateTimeOffset.UtcNow;
        var oldId = database.NewSessionId();
        var newId = database.NewSessionId();
        await repository.AddAsync(new ChatSession { SessionId = oldId, UserId = userId, Title = "Old", UpdatedAt = now.AddHours(-2) }, CancellationToken.None);
        await repository.AddAsync(new ChatSession { SessionId = newId, UserId = userId, Title = "New", UpdatedAt = now }, CancellationToken.None);
        await repository.AddAsync(new ChatSession { SessionId = database.NewSessionId(), UserId = database.NewUserId(), Title = "Foreign", UpdatedAt = now }, CancellationToken.None);
        await new OrchestratorUnitOfWork(context).SaveChangesAsync(CancellationToken.None);

        var summaries = await repository.ListSummariesByUserAsync(userId, 0, 10, CancellationToken.None);

        summaries.Select(s => s.SessionId).Should().Equal(newId, oldId);
        summaries[0].Title.Should().Be("New");
    }

    [CosmosFact]
    public async Task ListSummariesByUserAsync_WhenSkipAndTakeAreSet_ShouldReturnRequestedPage()
    {
        await using var database = await CosmosTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new ChatSessionRepository(context);
        var userId = database.NewUserId();
        var now = DateTimeOffset.UtcNow;
        var ids = Enumerable.Range(0, 5).Select(_ => database.NewSessionId()).ToList();
        for (var index = 0; index < ids.Count; index++)
        {
            await repository.AddAsync(
                new ChatSession { SessionId = ids[index], UserId = userId, UpdatedAt = now.AddMinutes(-index) },
                CancellationToken.None);
        }

        await new OrchestratorUnitOfWork(context).SaveChangesAsync(CancellationToken.None);

        var page = await repository.ListSummariesByUserAsync(userId, 2, 2, CancellationToken.None);

        page.Select(s => s.SessionId).Should().Equal(ids[2], ids[3]);
    }

    [CosmosFact]
    public async Task Delete_WhenSessionIsCommitted_ShouldRemoveSessionAndMessages()
    {
        await using var database = await CosmosTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new ChatSessionRepository(context);
        var unitOfWork = new OrchestratorUnitOfWork(context);
        var session = new ChatSession
        {
            SessionId = database.NewSessionId(),
            UserId = database.NewUserId(),
            Messages = [new ChatSessionMessage { Role = "user", Content = "hi" }],
        };
        await repository.AddAsync(session, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        repository.Delete(session);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        await using var otherContext = database.CreateContext();
        (await new ChatSessionRepository(otherContext).FindForUserAsync(session.SessionId, session.UserId, CancellationToken.None))
            .Should().BeNull();
    }

    [CosmosFact]
    public async Task AddAsync_WhenSessionHasSummaryFields_ShouldPersistThemIncludingTimestamp()
    {
        await using var database = await CosmosTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var updatedAt = DateTimeOffset.UtcNow;
        var session = new ChatSession
        {
            SessionId = database.NewSessionId(),
            UserId = database.NewUserId(),
            AgentId = "agent-1",
            Summary = "Short summary",
            SummarizedMessageCount = 8,
            UpdatedAt = updatedAt,
        };
        await new ChatSessionRepository(context).AddAsync(session, CancellationToken.None);
        await new OrchestratorUnitOfWork(context).SaveChangesAsync(CancellationToken.None);

        await using var otherContext = database.CreateContext();
        var persisted = await new ChatSessionRepository(otherContext)
            .FindForUserAsync(session.SessionId, session.UserId, CancellationToken.None);

        persisted!.AgentId.Should().Be("agent-1");
        persisted.Summary.Should().Be("Short summary");
        persisted.SummarizedMessageCount.Should().Be(8);
        persisted.UpdatedAt.UtcTicks.Should().Be(updatedAt.UtcTicks);
    }
}
