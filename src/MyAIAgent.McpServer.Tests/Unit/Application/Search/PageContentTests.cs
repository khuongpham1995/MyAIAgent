/**
 * MyAIAgent | McpServer · Tests
 *
 * Verifies the web-search page-content contract preserves its URL and text.
 */

using FluentAssertions;
using MyAIAgent.McpServer.Application.Search;

namespace MyAIAgent.McpServer.Tests.Unit.Application.Search;

public sealed class PageContentTests
{
    [Fact]
    public void Constructor_WhenGivenUrlAndContent_ShouldSetPropertiesCorrectly()
    {
        // Arrange & Act
        var page = new PageContent("https://example.com", "Page body text");

        // Assert
        page.Url.Should().Be("https://example.com");
        page.Content.Should().Be("Page body text");
    }

    [Fact]
    public void Equality_WhenSameValues_ShouldBeEqual()
    {
        // Arrange
        var a = new PageContent("https://example.com", "body");
        var b = new PageContent("https://example.com", "body");

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_WhenDifferentUrl_ShouldNotBeEqual()
    {
        // Arrange
        var a = new PageContent("https://example.com", "body");
        var b = new PageContent("https://other.com", "body");

        // Act & Assert
        a.Should().NotBe(b);
    }
}
