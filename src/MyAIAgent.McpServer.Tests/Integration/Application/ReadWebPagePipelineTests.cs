/**
 * MyAIAgent | McpServer · Tests
 *
 * Verifies URL rejection, normalized extraction, bounded Markdown, and provider failure through real MediatR dispatch.
 */

using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.McpServer.Application;
using MyAIAgent.McpServer.Application.Common.Interfaces;
using MyAIAgent.McpServer.Application.Search.Queries.ReadWebPage;

namespace MyAIAgent.McpServer.Tests.Integration.Application;

public sealed class ReadWebPagePipelineTests
{
    [Theory]
    [InlineData("")]
    [InlineData("http://example.com/")]
    [InlineData("https://user:password@example.com/")]
    [InlineData("https://example.com/?token=private")]
    [InlineData("https://example.com:444/")]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://8.8.8.8/")]
    [InlineData("https://[::1]/")]
    [InlineData("https://localhost/")]
    [InlineData("https://host.local/")]
    [InlineData("https://host.internal/")]
    [InlineData("https://host/")]
    [InlineData("file:///private/file")]
    [InlineData("https://example.com/line\nsecret")]
    public async Task Send_WhenUrlIsUnsupported_ShouldRejectBeforeReader(string url)
    {
        // Arrange
        var reader = new Mock<IWebPageReader>(MockBehavior.Strict);
        using var provider = Provider(reader.Object);

        // Act
        var action = () => provider.GetRequiredService<ISender>().Send(new ReadWebPageQuery(url));

        // Assert
        await action.Should().ThrowAsync<ValidationException>();
        reader.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(8000, false)]
    [InlineData(8001, true)]
    public async Task Send_WhenExtracted_ShouldNormalizeBoundAndTimestamp(int length, bool truncated)
    {
        // Arrange
        var reader = new Mock<IWebPageReader>();
        reader.Setup(r => r.ReadAsync(new Uri("https://example.com/article"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<string>.Succeed(new string('x', length)));
        using var provider = Provider(reader.Object);

        // Act
        var result = await provider.GetRequiredService<ISender>().Send(new ReadWebPageQuery("https://example.com/article#section"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Url.Should().Be("https://example.com/article");
        result.Data.Content.Length.Should().Be(Math.Min(8000, length));
        result.Data.Truncated.Should().Be(truncated);
        result.Data.RetrievedAtUtc.Should().Be(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public async Task Send_WhenBoundaryContainsSurrogatePair_ShouldKeepUnicodeIntact()
    {
        // Arrange
        var reader = new Mock<IWebPageReader>();
        reader.Setup(r => r.ReadAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<string>.Succeed(new string('x', 7999) + "😀"));
        using var provider = Provider(reader.Object);

        // Act
        var result = await provider.GetRequiredService<ISender>().Send(new ReadWebPageQuery("https://example.com/"));

        // Assert
        result.Data!.Content.Should().HaveLength(7999);
        result.Data.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task Send_WhenUrlExceedsLimit_ShouldRejectBeforeReader()
    {
        // Arrange
        var reader = new Mock<IWebPageReader>(MockBehavior.Strict);
        using var provider = Provider(reader.Object);

        // Act
        var action = () => provider.GetRequiredService<ISender>().Send(new ReadWebPageQuery("https://example.com/" + new string('x', 2048)));

        // Assert
        await action.Should().ThrowAsync<ValidationException>();
        reader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Send_WhenProviderFails_ShouldReturnFailureWithoutEvidence()
    {
        // Arrange
        var reader = new Mock<IWebPageReader>();
        reader.Setup(r => r.ReadAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<string>.Fail(message: "The webpage could not be read."));
        using var provider = Provider(reader.Object);

        // Act
        var result = await provider.GetRequiredService<ISender>().Send(new ReadWebPageQuery("https://example.com/"));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
    }

    private static ServiceProvider Provider(IWebPageReader reader)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<TimeProvider>(new FixedClock());
        services.AddSingleton(reader);
        return services.BuildServiceProvider();
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }
}
