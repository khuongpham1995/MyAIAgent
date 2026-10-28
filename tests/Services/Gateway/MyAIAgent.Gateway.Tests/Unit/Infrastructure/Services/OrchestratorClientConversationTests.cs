/**
 * MyAIAgent | Gateway · Tests
 *
 * Checks downstream conversation URLs, escaped inputs, response handling, and cancellation.
 */

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MyAIAgent.Gateway.Infrastructure.Services;

namespace MyAIAgent.Gateway.Tests.Unit.Infrastructure.Services;

public sealed class OrchestratorClientConversationTests
{
    private static OrchestratorClient BuildClient(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://orchestrator.test") },
            NullLogger<OrchestratorClient>.Instance);

    [Fact]
    public async Task ListConversationsAsync_WhenOrchestratorResponds_ShouldMapSummariesAndEscapeUserId()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                data = new[] { new { sessionId = "s-1", title = "Title", updatedAt = DateTimeOffset.UtcNow } },
            }),
        });

        // Act
        var result = await BuildClient(handler).ListConversationsAsync("visitor:a b", 1, 20, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().ContainSingle().Which.SessionId.Should().Be("s-1");
        handler.Requests.Single().RequestUri!.Query.Should().Contain("userId=visitor%3Aa%20b");
    }

    [Fact]
    public async Task GetConversationAsync_WhenOrchestratorReturnsNotFound_ShouldFailWithNotFoundMessage()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var result = await BuildClient(handler).GetConversationAsync("s-1", "visitor:a", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Conversation not found.");
    }

    [Fact]
    public async Task RenameConversationAsync_WhenOrchestratorFails_ShouldReturnSafeMessage()
    {
        // Arrange
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("10.0.0.5 refused"));

        // Act
        var result = await BuildClient(handler).RenameConversationAsync("s-1", "visitor:a", "Title", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Conversation rename failed.");
    }

    [Fact]
    public async Task DeleteConversationAsync_WhenOrchestratorReturnsNotFound_ShouldFailWithNotFoundMessage()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var result = await BuildClient(handler).DeleteConversationAsync("s-1", "visitor:a", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Conversation not found.");
    }
}
