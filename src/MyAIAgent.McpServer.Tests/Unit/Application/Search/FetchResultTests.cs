using FluentAssertions;
using MyAIAgent.McpServer.Application.Search;

namespace MyAIAgent.McpServer.Tests.Unit.Application.Search;

public sealed class FetchResultTests
{
    [Fact]
    public void Constructor_WhenSuccessWithContent_ShouldSetPropertiesCorrectly()
    {
        // Arrange & Act
        var result = new FetchResult(true, "Hello world", "text/html");

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Be("Hello world");
        result.ContentType.Should().Be("text/html");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Constructor_WhenFailureWithError_ShouldSetPropertiesCorrectly()
    {
        // Arrange & Act
        var result = new FetchResult(false, Error: "Connection refused");

        // Assert
        result.Success.Should().BeFalse();
        result.Content.Should().BeNull();
        result.ContentType.Should().BeNull();
        result.Error.Should().Be("Connection refused");
    }

    [Fact]
    public void Equality_WhenSameValues_ShouldBeEqual()
    {
        // Arrange
        var a = new FetchResult(true, "content", "text/plain");
        var b = new FetchResult(true, "content", "text/plain");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_WhenDifferentSuccess_ShouldNotBeEqual()
    {
        // Arrange
        var a = new FetchResult(true, "content", "text/plain");
        var b = new FetchResult(false, "content", "text/plain");

        // Assert
        a.Should().NotBe(b);
    }
}
