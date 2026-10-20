/**
 * MyAIAgent | McpServer · Tests
 *
 * Exercises MediatR web-search validation and handler dispatch with substituted external providers.
 */

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
using MyAIAgent.McpServer.Application.Common.Interfaces;
using MyAIAgent.McpServer.Application.Search;

namespace MyAIAgent.McpServer.Tests.Integration.Application;

public sealed class WebSearchPipelineTests
{
    [Fact]
    public async Task Handle_WhenValidQueryDispatchedThroughFullPipeline_ShouldReturnNonEmptyJsonString()
    {
        // Arrange
        var mockSearchProvider = new Mock<ISearchProvider>();
        var mockPageFetcher = new Mock<IPageFetcher>();

        mockSearchProvider
            .Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerableHelper.ToAsyncEnumerable([
                new SearchEntry("https://example.com/result1", "Result snippet one")
            ]));

        mockPageFetcher
            .Setup(w => w.GetContentAsync("https://example.com/result1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FetchResult(true, "Full page content", "text/html"));

        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(WebSearchHandler).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(typeof(WebSearchHandler).Assembly, includeInternalTypes: true);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddSingleton(mockSearchProvider.Object);
        services.AddSingleton(mockPageFetcher.Object);

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var query = new WebSearchQuery("integration test query", Top: 1);

        // Act
        var result = await sender.Send(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().NotBe("[]");
        result.Should().Contain("https://example.com/result1");
    }
}
