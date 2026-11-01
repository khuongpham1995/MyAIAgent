/**
 * MyAIAgent | McpServer · Tests
 *
 * Checks that search JSON retains its existing URL/content fields alongside evidence metadata.
 */

using System.Text.Json;
using FluentAssertions;
using MyAIAgent.McpServer.Application.Search;

namespace MyAIAgent.McpServer.Tests.Unit.Application.Search;

public sealed class PageContentTests
{
    [Fact]
    public void Serialize_WhenEvidenceIsReturned_ShouldPreserveCompatibleFields()
    {
        // Arrange
        var page = new PageContent("https://example.com/", "Excerpt", "Title", DateTimeOffset.UnixEpoch, true);

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new[] { page }));

        // Assert
        var item = json.RootElement[0];
        item.GetProperty("Url").GetString().Should().Be(page.Url);
        item.GetProperty("Content").GetString().Should().Be("Excerpt");
        item.GetProperty("Title").GetString().Should().Be("Title");
        item.GetProperty("RetrievedAtUtc").GetDateTimeOffset().Should().Be(DateTimeOffset.UnixEpoch);
        item.GetProperty("Truncated").GetBoolean().Should().BeTrue();
    }
}
