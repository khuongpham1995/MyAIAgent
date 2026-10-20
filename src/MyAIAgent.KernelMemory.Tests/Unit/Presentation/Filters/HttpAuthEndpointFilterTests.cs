/**
 * MyAIAgent | KernelMemory · Tests
 *
 * Checks enabled/disabled service authentication, both key slots, and missing or invalid credentials.
 */

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.KernelMemory.Configuration;
using MyAIAgent.KernelMemory.Presentation.Filters;

namespace MyAIAgent.KernelMemory.Tests.Unit.Presentation.Filters;

public sealed class HttpAuthEndpointFilterTests
{
    private const string ValidKey1 = "test-access-key-1-abcdefghijklmnop";
    private const string ValidKey2 = "test-access-key-2-abcdefghijklmnop";
    private const string HeaderName = "Authorization";

    [Fact]
    public async Task InvokeAsync_WhenAuthDisabled_ShouldCallNext()
    {
        // Arrange
        var config = new ServiceAuthorizationConfig { Enabled = false };
        var filter = new HttpAuthEndpointFilter(config, NullLogger<HttpAuthEndpointFilter>.Instance);
        var httpContext = new DefaultHttpContext();
        var context = CreateContext(httpContext);
        var expectedResult = Results.Ok();
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(expectedResult);

        // Act
        var result = await filter.InvokeAsync(context, next);

        // Assert
        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task InvokeAsync_WhenValidAccessKey1_ShouldCallNext()
    {
        // Arrange
        var config = new ServiceAuthorizationConfig
        {
            Enabled = true,
            HttpHeaderName = HeaderName,
            AccessKey1 = ValidKey1,
            AccessKey2 = ValidKey2
        };
        var filter = new HttpAuthEndpointFilter(config, NullLogger<HttpAuthEndpointFilter>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HeaderName] = ValidKey1;
        var context = CreateContext(httpContext);
        var expectedResult = Results.Ok();
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(expectedResult);

        // Act
        var result = await filter.InvokeAsync(context, next);

        // Assert
        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task InvokeAsync_WhenValidAccessKey2_ShouldCallNext()
    {
        // Arrange
        var config = new ServiceAuthorizationConfig
        {
            Enabled = true,
            HttpHeaderName = HeaderName,
            AccessKey1 = ValidKey1,
            AccessKey2 = ValidKey2
        };
        var filter = new HttpAuthEndpointFilter(config, NullLogger<HttpAuthEndpointFilter>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HeaderName] = ValidKey2;
        var context = CreateContext(httpContext);
        var expectedResult = Results.Ok();
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(expectedResult);

        // Act
        var result = await filter.InvokeAsync(context, next);

        // Assert
        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderMissing_ShouldReturn401()
    {
        // Arrange
        var config = new ServiceAuthorizationConfig
        {
            Enabled = true,
            HttpHeaderName = HeaderName,
            AccessKey1 = ValidKey1,
            AccessKey2 = ValidKey2
        };
        var filter = new HttpAuthEndpointFilter(config, NullLogger<HttpAuthEndpointFilter>.Instance);
        var httpContext = new DefaultHttpContext();
        var context = CreateContext(httpContext);
        EndpointFilterDelegate next = _ => throw new InvalidOperationException("Should not reach next");

        // Act
        var result = await filter.InvokeAsync(context, next);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result!;
        problem.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_WhenInvalidKey_ShouldReturn403()
    {
        // Arrange
        var config = new ServiceAuthorizationConfig
        {
            Enabled = true,
            HttpHeaderName = HeaderName,
            AccessKey1 = ValidKey1,
            AccessKey2 = ValidKey2
        };
        var filter = new HttpAuthEndpointFilter(config, NullLogger<HttpAuthEndpointFilter>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HeaderName] = "wrong-key";
        var context = CreateContext(httpContext);
        EndpointFilterDelegate next = _ => throw new InvalidOperationException("Should not reach next");

        // Act
        var result = await filter.InvokeAsync(context, next);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result!;
        problem.StatusCode.Should().Be(403);
    }

    #region Private Method(s)


    private static EndpointFilterInvocationContext CreateContext(HttpContext httpContext)
        => new TestEndpointFilterInvocationContext(httpContext);

    private sealed class TestEndpointFilterInvocationContext(HttpContext httpContext) : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext => httpContext;
        public override IList<object?> Arguments => [];
        public override T GetArgument<T>(int index) => throw new NotSupportedException();
    }

    #endregion
}
