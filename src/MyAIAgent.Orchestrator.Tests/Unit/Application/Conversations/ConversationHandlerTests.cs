using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyAIAgent.Orchestrator.Application;
using MyAIAgent.Orchestrator.Application.Common.Interfaces;
using MyAIAgent.Orchestrator.Application.Conversations;
using MyAIAgent.Orchestrator.Application.Conversations.Commands.DeleteConversation;
using MyAIAgent.Orchestrator.Application.Conversations.Commands.RenameConversation;
using MyAIAgent.Orchestrator.Application.Conversations.Queries.GetConversation;
using MyAIAgent.Orchestrator.Application.Conversations.Queries.ListConversations;
using MyAIAgent.Orchestrator.Domain.Entities;

namespace MyAIAgent.Orchestrator.Tests.Unit.Application.Conversations;

public sealed class ConversationHandlerTests
{
    private readonly Mock<IChatSessionRepository> sessionRepository = new();
    private readonly Mock<IUnitOfWork> unitOfWork = new();

    // Lookups are scoped to the owner, so only the owner's id finds the session; any other user gets null.
    private void SetupSession(ChatSession session) =>
        sessionRepository
            .Setup(r => r.FindForUserAsync("s-1", session.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

    [Fact]
    public async Task ListHandle_WhenPageTwo_ShouldSkipPreviousPage()
    {
        IReadOnlyList<ConversationSummary> expected = [new("s-1", "Title", DateTimeOffset.UtcNow)];
        sessionRepository
            .Setup(r => r.ListSummariesByUserAsync("visitor:a", 20, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var handler = new ListConversationsHandler(sessionRepository.Object);

        var result = await handler.Handle(new ListConversationsQuery("visitor:a", 2, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Equal(expected);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task ListValidate_WhenPagingIsOutOfRange_ShouldFail(int page, int pageSize)
    {
        var validator = new ListConversationsQueryValidator();

        var result = await validator.ValidateAsync(new ListConversationsQuery("visitor:a", page, pageSize));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetHandle_WhenOwner_ShouldReturnMessagesInChronologicalOrder()
    {
        var baseTime = DateTimeOffset.UtcNow;
        SetupSession(new ChatSession
        {
            SessionId = "s-1",
            UserId = "visitor:a",
            Title = "Chat",
            Messages =
            [
                new ChatSessionMessage { Role = "assistant", Content = "second", CreatedAt = baseTime.AddSeconds(1) },
                new ChatSessionMessage { Role = "user", Content = "first", CreatedAt = baseTime },
            ],
        });
        var handler = new GetConversationHandler(sessionRepository.Object, CreateMapper());

        var result = await handler.Handle(new GetConversationQuery("s-1", "visitor:a"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Messages.Select(m => m.Content).Should().Equal("first", "second");
    }

    [Fact]
    public async Task GetHandle_WhenSessionBelongsToAnotherUser_ShouldReportNotFound()
    {
        SetupSession(new ChatSession { SessionId = "s-1", UserId = "visitor:owner" });
        var handler = new GetConversationHandler(sessionRepository.Object, CreateMapper());

        var result = await handler.Handle(new GetConversationQuery("s-1", "visitor:other"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Conversation not found.");
    }

    [Fact]
    public async Task RenameHandle_WhenOwner_ShouldTrimTitleAndSave()
    {
        var session = new ChatSession { SessionId = "s-1", UserId = "visitor:a", Title = "Old" };
        SetupSession(session);
        var handler = new RenameConversationHandler(sessionRepository.Object, unitOfWork.Object, TimeProvider.System);

        var result = await handler.Handle(
            new RenameConversationCommand("s-1", "visitor:a", "  New name  "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        session.Title.Should().Be("New name");
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RenameHandle_WhenSessionBelongsToAnotherUser_ShouldNotChangeOrSave()
    {
        var session = new ChatSession { SessionId = "s-1", UserId = "visitor:owner", Title = "Old" };
        SetupSession(session);
        var handler = new RenameConversationHandler(sessionRepository.Object, unitOfWork.Object, TimeProvider.System);

        var result = await handler.Handle(
            new RenameConversationCommand("s-1", "visitor:other", "Hijack"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        session.Title.Should().Be("Old");
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameValidate_WhenTitleIsBlank_ShouldFail(string title)
    {
        var validator = new RenameConversationCommandValidator();

        var result = await validator.ValidateAsync(new RenameConversationCommand("s-1", "visitor:a", title));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RenameValidate_WhenTitleExceedsLimit_ShouldFail()
    {
        var validator = new RenameConversationCommandValidator();

        var result = await validator.ValidateAsync(
            new RenameConversationCommand("s-1", "visitor:a", new string('x', 101)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteHandle_WhenOwner_ShouldDeleteAndSave()
    {
        var session = new ChatSession { SessionId = "s-1", UserId = "visitor:a" };
        SetupSession(session);
        var handler = new DeleteConversationHandler(sessionRepository.Object, unitOfWork.Object);

        var result = await handler.Handle(new DeleteConversationCommand("s-1", "visitor:a"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sessionRepository.Verify(r => r.Delete(session), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteHandle_WhenSessionBelongsToAnotherUser_ShouldNotDelete()
    {
        SetupSession(new ChatSession { SessionId = "s-1", UserId = "visitor:owner" });
        var handler = new DeleteConversationHandler(sessionRepository.Object, unitOfWork.Object);

        var result = await handler.Handle(new DeleteConversationCommand("s-1", "visitor:other"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        sessionRepository.Verify(r => r.Delete(It.IsAny<ChatSession>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteHandle_WhenSessionMissing_ShouldReportNotFound()
    {
        var handler = new DeleteConversationHandler(sessionRepository.Object, unitOfWork.Object);

        var result = await handler.Handle(new DeleteConversationCommand("s-1", "visitor:a"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    // The repository is scoped to the owner, but the handlers keep their own check; these tests make a
    // misbehaving repository hand back someone else's session to prove the second line of defense holds.
    private void SetupRepositoryReturningForeignSession(ChatSession foreignSession) =>
        sessionRepository
            .Setup(r => r.FindForUserAsync("s-1", "visitor:other", It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignSession);

    [Fact]
    public async Task GetHandle_WhenRepositoryReturnsAnotherUsersSession_ShouldStillReportNotFound()
    {
        SetupRepositoryReturningForeignSession(new ChatSession { SessionId = "s-1", UserId = "visitor:owner" });

        var result = await new GetConversationHandler(sessionRepository.Object, CreateMapper())
            .Handle(new GetConversationQuery("s-1", "visitor:other"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Conversation not found.");
    }

    [Fact]
    public async Task RenameHandle_WhenRepositoryReturnsAnotherUsersSession_ShouldNotChangeOrSave()
    {
        var foreign = new ChatSession { SessionId = "s-1", UserId = "visitor:owner", Title = "Old" };
        SetupRepositoryReturningForeignSession(foreign);

        var result = await new RenameConversationHandler(sessionRepository.Object, unitOfWork.Object, TimeProvider.System)
            .Handle(new RenameConversationCommand("s-1", "visitor:other", "Hijack"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        foreign.Title.Should().Be("Old");
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteHandle_WhenRepositoryReturnsAnotherUsersSession_ShouldNotDeleteOrSave()
    {
        var foreign = new ChatSession { SessionId = "s-1", UserId = "visitor:owner" };
        SetupRepositoryReturningForeignSession(foreign);

        var result = await new DeleteConversationHandler(sessionRepository.Object, unitOfWork.Object)
            .Handle(new DeleteConversationCommand("s-1", "visitor:other"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        sessionRepository.Verify(r => r.Delete(It.IsAny<ChatSession>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Map_WhenRenameRequestHasRouteAndBody_ShouldBuildCommand()
    {
        var mapper = CreateMapper();

        var command = mapper.Map<RenameConversationCommand>(
            new RenameConversationRequest("s-1", new RenameConversationRequestDto("visitor:a", "Title")));

        command.Should().Be(new RenameConversationCommand("s-1", "visitor:a", "Title"));
    }

    private static IMapper CreateMapper()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }
}
