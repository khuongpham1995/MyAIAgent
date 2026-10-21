/**
 * MyAIAgent | McpServer · Tests
 *
 * Covers evidence ordering, source filtering, output bounds, empty results, and explicit failures.
 */

using FluentAssertions;
using Moq;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.McpServer.Application.Common.Interfaces;
using MyAIAgent.McpServer.Application.Search;
using MyAIAgent.McpServer.Application.Search.Queries.WebSearch;

namespace MyAIAgent.McpServer.Tests.Unit.Application.Search.Queries.WebSearch;

public sealed class WebSearchHandlerTests
{
    [Fact]
    public async Task Handle_WhenSourcesRepeatOrAreInvalid_ShouldKeepRankedUniqueSources()
    {
        // Arrange
        var provider = Provider([
            new("https://example.com/first", "First", "one"),
            new("https://example.com/first", "Duplicate", "duplicate"),
            new("file:///etc/passwd", "File", "bad"),
            new("https://user:password@example.com/", "Credentials", "bad"),
            new("https://example.com:8443/", "Port", "bad"),
            new(null, "Missing", "bad"),
            new("https://example.com/" + new string('a', 2048), "Long", "bad"),
            new("https://example.com/second", "Second", "two"),
            new("https://example.com/third", "Third", "three")]);
        var handler = new WebSearchHandler(provider.Object, new FixedClock());

        // Act
        var result = await handler.Handle(new WebSearchQuery("query", 2), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Select(x => x.Title).Should().Equal("First", "Second");
        result.Data!.Select(x => x.Content).Should().Equal("one", "two");
        result.Data.Should().OnlyContain(x => x.RetrievedAtUtc == DateTimeOffset.UnixEpoch && !x.Truncated);
    }

    [Fact]
    public async Task Handle_WhenExcerptsAreLarge_ShouldBoundEachAndCombinedContent()
    {
        // Arrange
        var provider = Provider(Enumerable.Range(0, 10)
            .Select(i => new SearchEntry($"https://example.com/{i}", new string('t', 300), new string('x', 4000))).ToArray());
        var handler = new WebSearchHandler(provider.Object, new FixedClock());

        // Act
        var result = await handler.Handle(new WebSearchQuery("query", 10), CancellationToken.None);

        // Assert
        result.Data.Should().HaveCount(7);
        result.Data!.Sum(x => x.Content.Length).Should().Be(20_000);
        result.Data.Should().OnlyContain(x => x.Content.Length <= 3000 && x.Title.Length == 256 && x.Truncated);
    }

    [Fact]
    public async Task Handle_WhenLimitSplitsUnicode_ShouldKeepSurrogatePairsIntact()
    {
        // Arrange
        var provider = Provider([new("https://example.com/", "Title", new string('x', 2999) + "😀tail")]);
        var handler = new WebSearchHandler(provider.Object, new FixedClock());

        // Act
        var result = await handler.Handle(new WebSearchQuery("query"), CancellationToken.None);

        // Assert
        result.Data!.Single().Content.Should().HaveLength(2999);
        result.Data!.Single().Truncated.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_WhenProviderIsEmptyOrFails_ShouldDistinguishTheOutcomes(bool success)
    {
        // Arrange
        var provider = new Mock<ISearchProvider>();
        provider.Setup(p => p.SearchAsync("query", 3, It.IsAny<CancellationToken>())).ReturnsAsync(success
            ? ResponseModel<IReadOnlyList<SearchEntry>>.Succeed([])
            : ResponseModel<IReadOnlyList<SearchEntry>>.Fail(message: "Web search timed out."));
        var handler = new WebSearchHandler(provider.Object, new FixedClock());

        // Act
        var result = await handler.Handle(new WebSearchQuery("query"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().Be(success);
        if (success) result.Data.Should().BeEmpty();
        else result.Message.Should().Be("Web search timed out.");
    }

    [Fact]
    public async Task Handle_WhenCallerCancels_ShouldPassTokenToProvider()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var provider = new Mock<ISearchProvider>();
        provider.Setup(p => p.SearchAsync("query", 3, cancellation.Token)).ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var handler = new WebSearchHandler(provider.Object, new FixedClock());
        cancellation.Cancel();

        // Act
        var action = () => handler.Handle(new WebSearchQuery("query"), cancellation.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static Mock<ISearchProvider> Provider(IReadOnlyList<SearchEntry> entries)
    {
        var provider = new Mock<ISearchProvider>();
        provider.Setup(p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<IReadOnlyList<SearchEntry>>.Succeed(entries));
        return provider;
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }
}
