/**
 * MyAIAgent | KernelMemory · Tests
 *
 * Checks upload-size response handling and rethrowing of unexpected KernelMemory endpoint failures.
 */

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using MyAIAgent.KernelMemory.Presentation.Filters;

namespace MyAIAgent.KernelMemory.Tests.Unit.Presentation.Filters;

public sealed class HttpErrorsEndpointFilterTests
{
    [Fact]
    public async Task InvokeAsync_WhenNextSucceeds_ShouldReturnNextResult()
    {
        // Arrange
        var filter = new HttpErrorsEndpointFilter(NullLogger<HttpErrorsEndpointFilter>.Instance);
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
    public async Task InvokeAsync_WhenBadHttpRequestExceptionWith413_ShouldReturnProblemResult()
    {
        // Arrange
        var filter = new HttpErrorsEndpointFilter(NullLogger<HttpErrorsEndpointFilter>.Instance);
        var httpContext = new DefaultHttpContext();
        var context = CreateContext(httpContext);
        EndpointFilterDelegate next = _ => throw new BadHttpRequestException("Request body too large.", 413);

        // Act
        var result = await filter.InvokeAsync(context, next);

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result!;
        problem.StatusCode.Should().Be(413);
    }

    [Fact]
    public async Task InvokeAsync_WhenOtherExceptionThrown_ShouldRethrow()
    {
        // Arrange
        var filter = new HttpErrorsEndpointFilter(NullLogger<HttpErrorsEndpointFilter>.Instance);
        var httpContext = new DefaultHttpContext();
        var context = CreateContext(httpContext);
        EndpointFilterDelegate next = _ => throw new InvalidOperationException("Unexpected failure");

        // Act
        var act = async () => await filter.InvokeAsync(context, next);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unexpected failure");
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
