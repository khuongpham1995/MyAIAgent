/**
 * MyAIAgent | Gateway · Tests
 *
 * Exercises conversation pagination, ownership, renaming, deletion, and validation in the owning service.
 */

using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Application;
using MyAIAgent.Gateway.Application.Common.Interfaces;
using MyAIAgent.Gateway.Application.Conversations;
using MyAIAgent.Gateway.Application.Conversations.Commands.DeleteConversation;
using MyAIAgent.Gateway.Application.Conversations.Commands.RenameConversation;
using MyAIAgent.Gateway.Application.Conversations.Queries.GetConversation;
using MyAIAgent.Gateway.Application.Conversations.Queries.ListConversations;

namespace MyAIAgent.Gateway.Tests.Unit.Application.Conversations;

public sealed class ConversationHandlerTests
{
    private readonly Mock<IOrchestratorClient> client = new();

    [Fact]
    public async Task ListHandle_WhenCalled_ShouldDelegateWithOwnerAndPaging()
    {
        // Arrange
        IReadOnlyList<ConversationSummary> summaries = [new("s-1", "Title", DateTimeOffset.UtcNow)];
        client
            .Setup(c => c.ListConversationsAsync("visitor:a", 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<IReadOnlyList<ConversationSummary>>.Succeed(summaries));

        // Act
        var result = await new ListConversationsHandler(client.Object)
            .Handle(new ListConversationsQuery("visitor:a", 2, 10), CancellationToken.None);

        // Assert
        result.Data.Should().Equal(summaries);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task ListValidate_WhenPagingIsOutOfRange_ShouldFail(int page, int pageSize)
    {
        // Arrange & Act
        var result = await new ListConversationsQueryValidator()
            .ValidateAsync(new ListConversationsQuery("visitor:a", page, pageSize));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    public async Task Validate_WhenSessionIdIsUnsafe_ShouldFailForEveryConversationRequest(string sessionId)
    {
        // Arrange & Act
        var get = await new GetConversationQueryValidator().ValidateAsync(new GetConversationQuery(sessionId, "visitor:a"));
        var rename = await new RenameConversationCommandValidator().ValidateAsync(new RenameConversationCommand(sessionId, "visitor:a", "T"));
        var delete = await new DeleteConversationCommandValidator().ValidateAsync(new DeleteConversationCommand(sessionId, "visitor:a"));

        // Assert
        new[] { get.IsValid, rename.IsValid, delete.IsValid }.Should().AllBeEquivalentTo(false);
    }

    [Fact]
    public async Task GetHandle_WhenCalled_ShouldDelegateWithOwner()
    {
        // Arrange
        var detail = new ConversationDetail("s-1", "Title", DateTimeOffset.UtcNow, []);
        client
            .Setup(c => c.GetConversationAsync("s-1", "visitor:a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<ConversationDetail>.Succeed(detail));

        // Act
        var result = await new GetConversationHandler(client.Object)
            .Handle(new GetConversationQuery("s-1", "visitor:a"), CancellationToken.None);

        // Assert
        result.Data.Should().Be(detail);
    }

    [Fact]
    public async Task RenameHandle_WhenCalled_ShouldDelegateWithOwnerAndTitle()
    {
        // Arrange
        client
            .Setup(c => c.RenameConversationAsync("s-1", "visitor:a", "New", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<bool>.Succeed(true));

        // Act
        var result = await new RenameConversationHandler(client.Object)
            .Handle(new RenameConversationCommand("s-1", "visitor:a", "New"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        client.VerifyAll();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameValidate_WhenTitleIsBlank_ShouldFail(string title)
    {
        // Arrange & Act
        var result = await new RenameConversationCommandValidator()
            .ValidateAsync(new RenameConversationCommand("s-1", "visitor:a", title));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RenameValidate_WhenTitleExceedsLimit_ShouldFail()
    {
        // Arrange & Act
        var result = await new RenameConversationCommandValidator()
            .ValidateAsync(new RenameConversationCommand("s-1", "visitor:a", new string('x', 101)));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteHandle_WhenCalled_ShouldDelegateWithOwner()
    {
        // Arrange
        client
            .Setup(c => c.DeleteConversationAsync("s-1", "visitor:a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<bool>.Succeed(true));

        // Act
        var result = await new DeleteConversationHandler(client.Object)
            .Handle(new DeleteConversationCommand("s-1", "visitor:a"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        client.VerifyAll();
    }

    [Fact]
    public void Map_WhenPublicRequestHasConfiguredAgent_ShouldIgnoreClientAgent()
    {
        // Arrange
        var mapper = CreateMapper();

        // Act
        var command = mapper.Map<MyAIAgent.Gateway.Application.Chat.Commands.StreamChatGateway.StreamChatGatewayCommand>(
            new MyAIAgent.Gateway.Application.Chat.PublicChatGatewayRequest(
                new MyAIAgent.Gateway.Application.Chat.PublicChatRequestDto("s-1", "Hi"),
                "visitor:a",
                "configured-agent"));

        // Assert
        command.AgentId.Should().Be("configured-agent");
        command.UserId.Should().Be("visitor:a");
    }

    [Fact]
    public void Map_WhenRenameRequestHasRouteAndBody_ShouldBuildCommand()
    {
        // Arrange & Act
        var command = CreateMapper().Map<RenameConversationCommand>(
            new RenameConversationRequest("s-1", "visitor:a", new RenameConversationRequestDto("Title")));

        // Assert
        command.Should().Be(new RenameConversationCommand("s-1", "visitor:a", "Title"));
    }

    private static IMapper CreateMapper()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }
}
