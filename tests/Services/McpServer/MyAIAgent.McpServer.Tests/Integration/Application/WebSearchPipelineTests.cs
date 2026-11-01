/**
 * MyAIAgent | McpServer · Tests
 *
 * Exercises real search validation and dispatch without contacting an external search provider.
 */

using MyAIAgent.Common.Application.Models;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyAIAgent.McpServer.Application;
using MyAIAgent.McpServer.Application.Common.Interfaces;
using MyAIAgent.McpServer.Application.Search;
using MyAIAgent.McpServer.Application.Search.Queries.WebSearch;

namespace MyAIAgent.McpServer.Tests.Integration.Application;

public sealed class WebSearchPipelineTests
{
    [Theory]
    [InlineData("query", 1, true)]
    [InlineData("", 1, false)]
    [InlineData("query", 11, false)]
    public async Task Send_WhenInputIsValidated_ShouldDispatchOnlyValidQueries(string query, int top, bool valid)
    {
        // Arrange
        var search = new Mock<ISearchProvider>();
        search.Setup(s => s.SearchAsync(query, top, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<IReadOnlyList<SearchEntry>>.Succeed([new("https://example.com/", "Title", "Excerpt")]));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(search.Object);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var action = () => sender.Send(new WebSearchQuery(query, top));

        // Assert
        if (valid)
        {
            var result = await action();
            result.IsSuccess.Should().BeTrue();
            result.Data!.Single().Content.Should().Be("Excerpt");
        }
        else await action.Should().ThrowAsync<ValidationException>();
        search.Verify(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), valid ? Times.Once : Times.Never);
    }
}
