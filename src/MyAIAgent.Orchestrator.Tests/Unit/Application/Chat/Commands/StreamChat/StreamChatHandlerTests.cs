/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Checks event order, turn commits, metadata fallback, provider failure, history summaries, and cancellation.
 */

using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Orchestrator.Application.Chat;
using MyAIAgent.Orchestrator.Application.Chat.Commands.StreamChat;
using MyAIAgent.Orchestrator.Application.Common.Exceptions;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Tests.Unit.Application.Chat.Commands.StreamChat;

public sealed class StreamChatHandlerTests
{
    private readonly Mock<IChatOrchestratorService> orchestrator = new();
    private readonly Mock<IChatSessionRepository> sessionRepository = new();
    private readonly Mock<IUnitOfWork> unitOfWork = new();
    private readonly Mock<IConversationTitleGenerator> titleGenerator = new();
    private readonly Mock<IConversationSummarizer> summarizer = new();

    private StreamChatHandler BuildHandler() => new(
        orchestrator.Object,
        sessionRepository.Object,
        unitOfWork.Object,
        titleGenerator.Object,
        summarizer.Object,
        TimeProvider.System,
        NullLogger<StreamChatHandler>.Instance);

    private void SetupStream(params string[] chunks) =>
        orchestrator
            .Setup(s => s.StreamChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => ToAsyncEnumerable(chunks));

    private static async Task<List<ChatStreamEvent>> CollectAsync(IAsyncEnumerable<ChatStreamEvent> events)
    {
        var collected = new List<ChatStreamEvent>();
        await foreach (var chatEvent in events)
            collected.Add(chatEvent);
        return collected;
    }

    [Fact]
    public async Task Handle_WhenNewSession_ShouldEmitSessionDeltasTitleAndDoneInOrder()
    {
        // Arrange
        SetupStream("Hel", "lo");
        titleGenerator
            .Setup(g => g.GenerateTitleAsync("Hi", "Hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Greeting");
        ChatSession? added = null;
        sessionRepository
            .Setup(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Callback<ChatSession, CancellationToken>((session, _) => added = session)
            .Returns(Task.CompletedTask);

        // Act
        var events = await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand(null, "Hi", "visitor:a", "agent-1"), CancellationToken.None));

        // Assert
        events.Select(e => e.Type).Should().Equal("session", "delta", "delta", "title", "done");
        events[0].SessionId.Should().Be(added!.SessionId);
        events[1].Text.Should().Be("Hel");
        events[2].Text.Should().Be("lo");
        events[3].Title.Should().Be("Greeting");
        events[4].SessionId.Should().Be(added.SessionId);
        added.Title.Should().Be("Greeting");
        added.AgentId.Should().Be("agent-1");
        added.Messages.Select(m => (m.Role, m.Content)).Should()
            .Equal(("user", "Hi"), ("assistant", "Hello"));
        // One save commits the turn, a second stores the generated title.
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenTitleIsGenerated_ShouldCommitTurnBeforeSlowTitleCall()
    {
        // Arrange
        SetupStream("Hello");
        var saveCountAtTitleTime = -1;
        var saves = 0;
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => saves++)
            .ReturnsAsync(1);
        titleGenerator
            .Setup(g => g.GenerateTitleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => saveCountAtTitleTime = saves)
            .ReturnsAsync("Greeting");

        // Act
        await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand(null, "Hi", "visitor:a"), CancellationToken.None));

        // Assert
        saveCountAtTitleTime.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTitleGenerationFails_ShouldKeepFallbackTitleAndStillComplete()
    {
        // Arrange
        SetupStream("Hello");
        titleGenerator
            .Setup(g => g.GenerateTitleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiProviderException("rate limited", new InvalidOperationException()));
        ChatSession? added = null;
        sessionRepository
            .Setup(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Callback<ChatSession, CancellationToken>((session, _) => added = session)
            .Returns(Task.CompletedTask);

        // Act
        var events = await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand(null, "Hi there", "visitor:a"), CancellationToken.None));

        // Assert
        events.Select(e => e.Type).Should().Equal("session", "delta", "title", "done");
        events[2].Title.Should().Be("Hi there");
        added!.Title.Should().Be("Hi there");
        added.Messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenProviderThrowsAiProviderException_ShouldEmitErrorAndPersistNothing()
    {
        // Arrange
        orchestrator
            .Setup(s => s.StreamChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => ThrowAfterAsync(new AiProviderException("429", new InvalidOperationException())));

        // Act
        var events = await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand(null, "Hi", "visitor:a"), CancellationToken.None));

        // Assert
        events.Select(e => e.Type).Should().Equal("session", "delta", "error");
        events[^1].Message.Should().Be("AI service temporarily unavailable.");
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAgentIsMissing_ShouldEmitAgentNotFoundErrorWithoutLeakingId()
    {
        // Arrange
        orchestrator
            .Setup(s => s.StreamChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => ThrowAfterAsync(new AgentNotFoundException("secret-agent-id")));

        // Act
        var events = await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand(null, "Hi", "visitor:a", "secret-agent-id"), CancellationToken.None));

        // Assert
        events[^1].Type.Should().Be("error");
        events[^1].Message.Should().Be("Agent not found.");
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSummarizerThrowsAiProviderException_ShouldStillCompleteTurn()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow.AddHours(-1);
        var session = new ChatSession { SessionId = "s-1", UserId = "visitor:a" };
        for (var index = 0; index < 18; index++)
        {
            session.Messages.Add(new ChatSessionMessage
            {
                Role = index % 2 == 0 ? "user" : "assistant",
                Content = $"m{index}",
                CreatedAt = baseTime.AddSeconds(index),
            });
        }

        sessionRepository
            .Setup(r => r.FindForUserAsync("s-1", "visitor:a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        SetupStream("answer");
        summarizer
            .Setup(s => s.SummarizeAsync(It.IsAny<string?>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiProviderException("429", new InvalidOperationException()));

        // Act
        var events = await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand("s-1", "question", "visitor:a"), CancellationToken.None));

        // Assert
        events.Select(e => e.Type).Should().Equal("session", "delta", "done");
        session.Summary.Should().BeNull();
        session.Messages.Should().HaveCount(20);
    }

    [Fact]
    public async Task Handle_WhenTitleSaveFails_ShouldStillCompleteWithFallbackTitleAndDone()
    {
        // Arrange
        SetupStream("Hello");
        titleGenerator
            .Setup(g => g.GenerateTitleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Generated");
        unitOfWork
            .SetupSequence(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .ThrowsAsync(new PersistenceException("throttled", new InvalidOperationException()));
        ChatSession? added = null;
        sessionRepository
            .Setup(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Callback<ChatSession, CancellationToken>((session, _) => added = session)
            .Returns(Task.CompletedTask);

        // Act
        var events = await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand(null, "Hi there", "visitor:a"), CancellationToken.None));

        // Assert
        events.Select(e => e.Type).Should().Equal("session", "delta", "title", "done");
        events[2].Title.Should().Be("Hi there");
        added!.Title.Should().Be("Hi there");
    }

    [Fact]
    public async Task Handle_WhenSummarySaveFails_ShouldStillEmitDoneAndKeepSummaryOff()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow.AddHours(-1);
        var session = new ChatSession { SessionId = "s-1", UserId = "visitor:a" };
        for (var index = 0; index < 18; index++)
        {
            session.Messages.Add(new ChatSessionMessage
            {
                Role = index % 2 == 0 ? "user" : "assistant",
                Content = $"m{index}",
                CreatedAt = baseTime.AddSeconds(index),
            });
        }

        sessionRepository
            .Setup(r => r.FindForUserAsync("s-1", "visitor:a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        SetupStream("answer");
        summarizer
            .Setup(s => s.SummarizeAsync(It.IsAny<string?>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Condensed.");
        unitOfWork
            .SetupSequence(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .ThrowsAsync(new PersistenceException("throttled", new InvalidOperationException()));

        // Act
        var events = await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand("s-1", "question", "visitor:a"), CancellationToken.None));

        // Assert
        events.Select(e => e.Type).Should().Equal("session", "delta", "done");
        session.Summary.Should().BeNull();
        session.SummarizedMessageCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCommittingTheTurnFails_ShouldPropagateSoNothingLooksSaved()
    {
        // Arrange
        SetupStream("Hello");
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PersistenceException("throttled", new InvalidOperationException()));

        // Act
        var act = async () => await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand(null, "Hi", "visitor:a"), CancellationToken.None));

        // Assert
        await act.Should().ThrowAsync<PersistenceException>();
    }

    [Fact]
    public async Task Handle_WhenExistingSession_ShouldNotGenerateTitle()
    {
        // Arrange
        var session = new ChatSession { SessionId = "s-1", UserId = "visitor:a", Title = "Kept" };
        sessionRepository
            .Setup(r => r.FindForUserAsync("s-1", "visitor:a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        SetupStream("Reply");

        // Act
        var events = await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand("s-1", "Again", "visitor:a"), CancellationToken.None));

        // Assert
        events.Select(e => e.Type).Should().Equal("session", "delta", "done");
        session.Title.Should().Be("Kept");
        titleGenerator.Verify(
            g => g.GenerateTitleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        sessionRepository.Verify(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionBelongsToAnotherUser_ShouldStartNewConversationWithoutTouchingTheOwnersSession()
    {
        // Arrange
        var ownersSession = new ChatSession { SessionId = "s-1", UserId = "visitor:owner" };
        sessionRepository
            .Setup(r => r.FindForUserAsync("s-1", "visitor:owner", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownersSession);
        ChatSession? added = null;
        sessionRepository
            .Setup(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
            .Callback<ChatSession, CancellationToken>((session, _) => added = session)
            .Returns(Task.CompletedTask);
        SetupStream("Fresh");

        // Act
        var events = await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand("s-1", "Peek", "visitor:other"), CancellationToken.None));

        // Assert
        events[0].Type.Should().Be("session");
        events[0].SessionId.Should().NotBe("s-1");
        events.Select(e => e.Type).Should().NotContain("error");
        added!.UserId.Should().Be("visitor:other");
        ownersSession.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenProviderFailsMidStream_ShouldEmitErrorAndPersistNothing()
    {
        // Arrange
        orchestrator
            .Setup(s => s.StreamChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => FailAfterAsync("partial"));

        // Act
        var events = await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand(null, "Hi", "visitor:a"), CancellationToken.None));

        // Assert
        events.Select(e => e.Type).Should().Equal("session", "delta", "error");
        events[^1].Message.Should().Be("AI service temporarily unavailable.");
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        sessionRepository.Verify(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProviderReturnsNothing_ShouldEmitErrorAndPersistNothing()
    {
        // Arrange
        SetupStream();

        // Act
        var events = await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand(null, "Hi", "visitor:a"), CancellationToken.None));

        // Assert
        events.Select(e => e.Type).Should().Equal("session", "error");
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCancelledDuringStream_ShouldPropagateAndPersistNothing()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        orchestrator
            .Setup(s => s.StreamChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() => CancelAfterAsync(cancellation));

        // Act
        var act = async () => await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand(null, "Hi", "visitor:a"), cancellation.Token));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        sessionRepository.Verify(r => r.AddAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionHasSummary_ShouldSendSummaryAndUnsummarizedMessages()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        var session = new ChatSession
        {
            SessionId = "s-1",
            UserId = "visitor:a",
            Summary = "User likes cats.",
            SummarizedMessageCount = 2,
            Messages =
            [
                new ChatSessionMessage { Role = "user", Content = "old-1", CreatedAt = baseTime },
                new ChatSessionMessage { Role = "assistant", Content = "old-2", CreatedAt = baseTime.AddSeconds(1) },
                new ChatSessionMessage { Role = "user", Content = "recent", CreatedAt = baseTime.AddSeconds(2) },
            ],
        };
        sessionRepository
            .Setup(r => r.FindForUserAsync("s-1", "visitor:a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        ChatRequest? captured = null;
        orchestrator
            .Setup(s => s.StreamChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChatRequest, CancellationToken>((request, _) => captured = request)
            .Returns(() => ToAsyncEnumerable(["ok"]));

        // Act
        await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand("s-1", "next", "visitor:a"), CancellationToken.None));

        // The summary is replayed as user content so visitor-derived text never gains system authority.

        // Assert
        captured!.History.Select(m => m.Role).Should().Equal("user", "user");
        captured.History[0].Content.Should().Contain("User likes cats.").And.Contain("not instructions");
        captured.History[1].Content.Should().Be("recent");
    }

    [Fact]
    public async Task Handle_WhenOlderMessagesReachThreshold_ShouldSummarizeAndSaveAgain()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow.AddHours(-1);
        var session = new ChatSession { SessionId = "s-1", UserId = "visitor:a", Title = "Long" };
        for (var index = 0; index < 18; index++)
        {
            session.Messages.Add(new ChatSessionMessage
            {
                Role = index % 2 == 0 ? "user" : "assistant",
                Content = $"m{index}",
                CreatedAt = baseTime.AddSeconds(index),
            });
        }

        sessionRepository
            .Setup(r => r.FindForUserAsync("s-1", "visitor:a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        SetupStream("answer");
        IReadOnlyList<ChatMessage>? folded = null;
        summarizer
            .Setup(s => s.SummarizeAsync(null, It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<string?, IReadOnlyList<ChatMessage>, CancellationToken>((_, messages, _) => folded = messages)
            .ReturnsAsync("Condensed.");

        // Act
        await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand("s-1", "question", "visitor:a"), CancellationToken.None));

        // 18 existing + 2 new = 20 unsummarized, 10 stay verbatim, 10 are folded.

        // Assert
        folded!.Select(m => m.Content).Should().Equal(Enumerable.Range(0, 10).Select(i => $"m{i}"));
        session.Summary.Should().Be("Condensed.");
        session.SummarizedMessageCount.Should().Be(10);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenSummarizerReturnsNothing_ShouldKeepHistoryAndSaveOnce()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow.AddHours(-1);
        var session = new ChatSession { SessionId = "s-1", UserId = "visitor:a" };
        for (var index = 0; index < 18; index++)
        {
            session.Messages.Add(new ChatSessionMessage
            {
                Role = index % 2 == 0 ? "user" : "assistant",
                Content = $"m{index}",
                CreatedAt = baseTime.AddSeconds(index),
            });
        }

        sessionRepository
            .Setup(r => r.FindForUserAsync("s-1", "visitor:a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        SetupStream("answer");
        summarizer
            .Setup(s => s.SummarizeAsync(It.IsAny<string?>(), It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        await CollectAsync(
            BuildHandler().Handle(new StreamChatCommand("s-1", "question", "visitor:a"), CancellationToken.None));

        // Assert
        session.Summary.Should().BeNull();
        session.SummarizedMessageCount.Should().Be(0);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(
        IEnumerable<string> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }

    private static async IAsyncEnumerable<string> FailAfterAsync(string firstChunk)
    {
        await Task.Yield();
        yield return firstChunk;
        throw new HttpRequestException("provider down");
    }

    private static async IAsyncEnumerable<string> ThrowAfterAsync(Exception exception)
    {
        await Task.Yield();
        yield return "partial";
        throw exception;
    }

    private static async IAsyncEnumerable<string> CancelAfterAsync(CancellationTokenSource cancellation)
    {
        await Task.Yield();
        yield return "partial";
        await cancellation.CancelAsync();
        cancellation.Token.ThrowIfCancellationRequested();
    }
}
