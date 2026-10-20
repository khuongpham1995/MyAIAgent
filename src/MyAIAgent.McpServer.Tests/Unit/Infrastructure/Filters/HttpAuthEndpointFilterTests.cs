/**
 * MyAIAgent | McpServer · Tests
 *
 * Checks enabled/disabled service authentication, both key slots, and missing or invalid credentials.
 */

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Common.Infrastructure.Filters;

namespace MyAIAgent.McpServer.Tests.Unit.Infrastructure.Filters;

public sealed class HttpAuthEndpointFilterTests
{
    private static HttpAuthEndpointFilter BuildFilter(ServiceAuthorizationModel model)
        => new(Options.Create(model), NullLogger<HttpAuthEndpointFilter>.Instance);

    private static EndpointFilterInvocationContext BuildContext(
        string? headerValue = null,
        string headerName = "Authorization")
    {
        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
            httpContext.Request.Headers[headerName] = headerValue;

        var mock = new Mock<EndpointFilterInvocationContext>();
        mock.Setup(c => c.HttpContext).Returns(httpContext);
        return mock.Object;
    }

    private static readonly EndpointFilterDelegate PassThrough =
        _ => ValueTask.FromResult<object?>(Results.Ok());

    [Fact]
    public async Task InvokeAsync_WhenAuthDisabled_ShouldPassThrough()
    {
        // Arrange
        var filter = BuildFilter(new ServiceAuthorizationModel { Enabled = false });
        var context = BuildContext();

        // Act
        var result = await filter.InvokeAsync(context, PassThrough);

        // Assert
        result.Should().BeOfType<Ok>();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthEnabledAndAccessKey1Matches_ShouldPassThrough()
    {
        // Arrange
        var filter = BuildFilter(new ServiceAuthorizationModel
        {
            Enabled = true,
            AccessKey1 = "key-one",
            AccessKey2 = "key-two"
        });
        var context = BuildContext("key-one");

        // Act
        var result = await filter.InvokeAsync(context, PassThrough);

        // Assert
        result.Should().BeOfType<Ok>();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthEnabledAndAccessKey2Matches_ShouldPassThrough()
    {
        // Arrange
        var filter = BuildFilter(new ServiceAuthorizationModel
        {
            Enabled = true,
            AccessKey1 = "key-one",
            AccessKey2 = "key-two"
        });
        var context = BuildContext("key-two");

        // Act
        var result = await filter.InvokeAsync(context, PassThrough);

        // Assert
        result.Should().BeOfType<Ok>();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthEnabledAndHeaderMissing_ShouldReturn401()
    {
        // Arrange
        var filter = BuildFilter(new ServiceAuthorizationModel
        {
            Enabled = true,
            AccessKey1 = "key-one",
            AccessKey2 = "key-two"
        });
        var context = BuildContext();

        // Act
        var result = await filter.InvokeAsync(context, PassThrough);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthEnabledAndKeyInvalid_ShouldReturn403()
    {
        // Arrange
        var filter = BuildFilter(new ServiceAuthorizationModel
        {
            Enabled = true,
            AccessKey1 = "key-one",
            AccessKey2 = "key-two"
        });
        var context = BuildContext("wrong-key");

        // Act
        var result = await filter.InvokeAsync(context, PassThrough);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_WhenCustomHeaderName_ShouldReadFromConfiguredHeader()
    {
        // Arrange
        var filter = BuildFilter(new ServiceAuthorizationModel
        {
            Enabled = true,
            AccessKey1 = "key-one",
            AccessKey2 = "key-two",
            HttpHeaderName = "X-Api-Key"
        });
        var context = BuildContext(headerValue: "key-one", headerName: "X-Api-Key");

        // Act
        var result = await filter.InvokeAsync(context, PassThrough);

        // Assert
        result.Should().BeOfType<Ok>();
    }

    [Fact]
    public void Constructor_WhenEnabledWithEmptyAccessKey1_ShouldThrow()
    {
        // Arrange
        var model = new ServiceAuthorizationModel
        {
            Enabled = true,
            AccessKey1 = "",
            AccessKey2 = "key-two"
        };

        // Act
        var act = () => BuildFilter(model);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AccessKey1*");
    }

    [Fact]
    public void Constructor_WhenEnabledWithEmptyAccessKey2_ShouldThrow()
    {
        // Arrange
        var model = new ServiceAuthorizationModel
        {
            Enabled = true,
            AccessKey1 = "key-one",
            AccessKey2 = ""
        };

        // Act
        var act = () => BuildFilter(model);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AccessKey2*");
    }
}
