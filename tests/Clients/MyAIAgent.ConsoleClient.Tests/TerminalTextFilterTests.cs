/**
 * MyAIAgent | Console Client · Tests
 *
 * Verifies that provider text cannot execute terminal controls, including sequences split across deltas.
 */

using FluentAssertions;

namespace MyAIAgent.ConsoleClient.Tests;

public sealed class TerminalTextFilterTests
{
    [Theory]
    [InlineData("Hello 🌏\nhttps://example.com", "Hello 🌏\nhttps://example.com")]
    [InlineData("before\u001b[2Jafter", "beforeafter")]
    [InlineData("before\u001b]52;c;clipboard\aafter", "beforeafter")]
    [InlineData("before\u001b]8;;https://evil.example\u001b\\label\u001b]8;;\u001b\\after", "beforelabelafter")]
    [InlineData("before\u009b2Jafter", "beforeafter")]
    [InlineData("before\r\b\a\u202eafter", "beforeafter")]
    [InlineData("before\u001bPprivate\u001b\\after", "beforeafter")]
    public void Filter_WhenTextContainsControls_ShouldRetainOnlyPrintableContent(string text, string expected)
    {
        // Arrange
        var filter = new TerminalTextFilter();

        // Act
        var result = filter.Filter(text);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Filter_WhenEscapeSequenceCrossesChunks_ShouldNotLeakControlPayload()
    {
        // Arrange
        var filter = new TerminalTextFilter();

        // Act
        var result = filter.Filter("safe\u001b]") + filter.Filter("52;c;secret\u001b") + filter.Filter("\\tail");

        // Assert
        result.Should().Be("safetail");
    }
}
