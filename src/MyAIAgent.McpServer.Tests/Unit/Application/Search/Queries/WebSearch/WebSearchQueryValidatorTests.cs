/**
 * MyAIAgent | McpServer · Tests
 *
 * Checks search input limits before provider calls can consume credits.
 */

using FluentAssertions;
using MyAIAgent.McpServer.Application.Search.Queries.WebSearch;

namespace MyAIAgent.McpServer.Tests.Unit.Application.Search.Queries.WebSearch;

public sealed class WebSearchQueryValidatorTests
{
    [Theory]
    [InlineData("", 3)]
    [InlineData("   ", 3)]
    [InlineData("query", 0)]
    [InlineData("query", 11)]
    public void Validate_WhenInputIsInvalid_ShouldReject(string query, int top)
    {
        // Arrange
        var validator = new WebSearchQueryValidator();

        // Act
        var result = validator.Validate(new WebSearchQuery(query, top));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Validate_WhenQueryReachesLimit_ShouldEnforceMaximum(int length, bool valid)
    {
        // Arrange
        var validator = new WebSearchQueryValidator();

        // Act
        var result = validator.Validate(new WebSearchQuery(new string('a', length)));

        // Assert
        result.IsValid.Should().Be(valid);
    }
}
