using FluentAssertions;
using MyAIAgent.McpServer.Application.Search;

namespace MyAIAgent.McpServer.Tests.Unit.Application.Search;

public sealed class SearchEntryTests
{
    [Fact]
    public void Constructor_WhenGivenLinkAndSnippet_ShouldSetPropertiesCorrectly()
    {
        // Arrange & Act
        var entry = new SearchEntry("https://example.com", "A brief description");

        // Assert
        entry.Link.Should().Be("https://example.com");
        entry.Snippet.Should().Be("A brief description");
    }

    [Fact]
    public void Constructor_WhenBothNullable_ShouldAllowNullValues()
    {
        // Arrange & Act
        var entry = new SearchEntry(null, null);

        // Assert
        entry.Link.Should().BeNull();
        entry.Snippet.Should().BeNull();
    }

    [Fact]
    public void Equality_WhenSameValues_ShouldBeEqual()
    {
        // Arrange
        var a = new SearchEntry("https://example.com", "snippet");
        var b = new SearchEntry("https://example.com", "snippet");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_WhenDifferentSnippet_ShouldNotBeEqual()
    {
        // Arrange
        var a = new SearchEntry("https://example.com", "snippet one");
        var b = new SearchEntry("https://example.com", "snippet two");

        // Assert
        a.Should().NotBe(b);
    }
}
