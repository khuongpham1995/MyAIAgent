/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Checks bounded source formatting and per-turn invocation budgets against duplicate, malformed, and unsafe evidence.
 */

using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using MyAIAgent.Orchestrator.Infrastructure.Helpers;

namespace MyAIAgent.Orchestrator.Tests.Unit.Infrastructure.Helpers;

public sealed class WebResearchSourcesTests
{
    [Fact]
    public void Footer_WhenEvidenceIsDuplicatedOrUnsafe_ShouldIncludeOnlySafeUniqueLinks()
    {
        // Arrange
        var sources = new WebResearchSources();
        var urls = new[] { "https://example.com/source", "https://example.com/source", "javascript:alert(1)", "http://127.0.0.1/", "https://user:secret@example.com/", "https://example.com:444/", "https://host.local/", "https://example.com/\nunsafe", "https://example.com/source?token=private" };

        // Act
        sources.Record("web_search", new TextContent(JsonSerializer.Serialize(urls.Select(url => new { Url = url, Content = "Evidence" }))));
        var footer = sources.Footer();

        // Assert
        footer.Should().Be("\n\n**Retrieved sources**\n- <https://example.com/source>\n");
    }

    [Fact]
    public void Footer_WhenToolFailsOrReturnsMalformedEvidence_ShouldNotProduceSources()
    {
        // Arrange
        var sources = new WebResearchSources();
        var evidence = JsonSerializer.Serialize(new { Url = "https://example.com/", Content = "Evidence" });
        var failed = JsonSerializer.SerializeToElement(new { isError = true, content = new[] { new { type = "text", text = evidence } } });

        // Act
        sources.Record("read_webpage", failed);
        sources.Record("read_webpage", "not json");
        sources.Record("read_webpage", "{\"Url\":\"https://example.com/\",\"Content\":\" \"}");
        sources.Record("other_tool", evidence);
        sources.Record("read_webpage", new string('x', 1024 * 1024 + 1));

        // Assert
        sources.Footer().Should().BeEmpty();
    }

    [Fact]
    public void Footer_WhenSourcesExceedBounds_ShouldLimitCountAndTotalCharacters()
    {
        // Arrange
        var countBound = new WebResearchSources();
        var sizeBound = new WebResearchSources();

        // Act
        countBound.Record("web_search", JsonSerializer.Serialize(Enumerable.Range(0, 30).Select(i => new { Url = $"https://example.com/{i}", Content = "Evidence" })));
        sizeBound.Record("web_search", JsonSerializer.Serialize(Enumerable.Range(0, 10).Select(i => new { Url = $"https://example.com/{i}" + new string('x', 1900), Content = "Evidence" })));

        // Assert
        countBound.Footer().Split("- <").Should().HaveCount(11);
        sizeBound.Footer().Length.Should().BeLessThanOrEqualTo(8000);
    }

    [Fact]
    public async Task InvokeAsync_WhenConcurrentCallsExceedBudget_ShouldRunOnlyThreeAndKeepTurnsIsolated()
    {
        // Arrange
        var calls = 0;
        var first = new WebResearchSources();
        var second = new WebResearchSources();
        var inner = AIFunctionFactory.Create(() =>
        {
            Interlocked.Increment(ref calls);
            return "[{\"Url\":\"https://example.com/\",\"Content\":\"Evidence\"}]";
        }, "web_search");
        var function = new WebResearchFunction(inner, first);

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () => await function.InvokeAsync(new AIFunctionArguments()))));
        var nextTurn = await new WebResearchFunction(inner, second).InvokeAsync(new AIFunctionArguments());

        // Assert
        calls.Should().Be(4);
        results.Count(result => result!.ToString()!.Contains("limit reached")).Should().Be(5);
        first.Footer().Should().Contain("https://example.com/");
        second.Footer().Should().Contain("https://example.com/");
        nextTurn!.ToString().Should().NotContain("limit reached");
    }

    [Fact]
    public async Task InvokeAsync_WhenCallsFail_ShouldStillConsumeBudget()
    {
        // Arrange
        var calls = 0;
        var sources = new WebResearchSources();
        var function = new WebResearchFunction(AIFunctionFactory.Create(() =>
        {
            calls++;
            return JsonSerializer.SerializeToElement(new { isError = true, content = new[] { new { type = "text", text = "Provider unavailable." } } });
        }, "read_webpage"), sources);

        // Act
        for (var i = 0; i < 4; i++) await function.InvokeAsync(new AIFunctionArguments());

        // Assert
        calls.Should().Be(3);
        sources.Footer().Should().BeEmpty();
    }
}
