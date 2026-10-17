using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Common.Tests;
using MyAIAgent.McpServer.Application.Search.Queries.WebSearch;
using MyAIAgent.McpServer.Application.Interfaces;
using MyAIAgent.McpServer.Domain.Models;

namespace MyAIAgent.McpServer.Tests.Unit.Application.Search.Queries.WebSearch;

public sealed class WebSearchHandlerTests
{
    [Fact]
    public async Task Handle_WhenBothFetchesSucceed_ShouldReturnJsonContainingBothUrls()
    {
        // Arrange
        var mockSearchProvider = new Mock<ISearchProvider>();
        var mockPageFetcher = new Mock<IPageFetcher>();

        mockSearchProvider
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerableHelper.ToAsyncEnumerable([
                new SearchEntry("https://example.com/page1", "Snippet one"),
                new SearchEntry("https://example.com/page2", "Snippet two")
            ]));

        mockPageFetcher
            .Setup(w => w.GetContentAsync("https://example.com/page1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FetchResult(true, "Content one", "text/plain"));

        mockPageFetcher
            .Setup(w => w.GetContentAsync("https://example.com/page2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FetchResult(true, "Content two", "text/plain"));

        var handler = new WebSearchHandler(mockSearchProvider.Object, mockPageFetcher.Object, NullLogger<WebSearchHandler>.Instance);
        var query = new WebSearchQuery("test query");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("https://example.com/page1");
        result.Should().Contain("https://example.com/page2");
    }

    [Fact]
    public async Task Handle_WhenSearchReturnsNoResults_ShouldReturnJsonForEmptyList()
    {
        // Arrange
        var mockSearchProvider = new Mock<ISearchProvider>();
        var mockPageFetcher = new Mock<IPageFetcher>();

        mockSearchProvider
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerableHelper.ToAsyncEnumerable(Array.Empty<SearchEntry>()));

        var handler = new WebSearchHandler(mockSearchProvider.Object, mockPageFetcher.Object, NullLogger<WebSearchHandler>.Instance);
        var query = new WebSearchQuery("empty query");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be("[]");
    }

    [Fact]
    public async Task Handle_WhenOnePageFetchThrows_ShouldSwallowFailureAndReturnOnlySuccessfulUrl()
    {
        // Arrange
        var mockSearchProvider = new Mock<ISearchProvider>();
        var mockPageFetcher = new Mock<IPageFetcher>();

        mockSearchProvider
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerableHelper.ToAsyncEnumerable([
                new SearchEntry("https://example.com/good", "Good snippet"),
                new SearchEntry("https://example.com/bad", "Bad snippet")
            ]));

        mockPageFetcher
            .Setup(w => w.GetContentAsync("https://example.com/good", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FetchResult(true, "Good content", "text/plain"));

        mockPageFetcher
            .Setup(w => w.GetContentAsync("https://example.com/bad", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var handler = new WebSearchHandler(mockSearchProvider.Object, mockPageFetcher.Object, NullLogger<WebSearchHandler>.Instance);
        var query = new WebSearchQuery("fetch failure query");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("https://example.com/good");
        result.Should().NotContain("https://example.com/bad");

        var deserialized = JsonSerializer.Deserialize<List<PageContent>>(result);
        deserialized.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenQueryIsEmpty_ShouldThrowValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(WebSearchHandler).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(typeof(WebSearchHandler).Assembly, includeInternalTypes: true);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddSingleton(new Mock<ISearchProvider>().Object);
        services.AddSingleton(new Mock<IPageFetcher>().Object);

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var act = async () => await sender.Send(new WebSearchQuery(""), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
