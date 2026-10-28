/**
 * MyAIAgent | Common · Infrastructure · Tests
 *
 * Verifies serialized error status codes match HTTP responses and hide internal exception details.
 */

using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MyAIAgent.Common.Infrastructure.Handlers;

namespace MyAIAgent.Gateway.Tests.Unit.Infrastructure;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_WhenValidationFails_ShouldKeepFieldErrorsAndMatchingStatus()
    {
        // Arrange
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var stream = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = stream;
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var exception = new ValidationException([
            new ValidationFailure("UserMessage", "A message is required."),
            new ValidationFailure("UserMessage", "A message must be short."),
        ]);

        // Act
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        stream.Position = 0;
        using var body = await JsonDocument.ParseAsync(stream);

        // Assert
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        body.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("message").GetString().Should().Be("Validation failed");
        body.RootElement.GetProperty("data").GetProperty("statusCode").GetInt32().Should().Be(400);
        body.RootElement.GetProperty("errors").GetProperty("UserMessage").GetString()
            .Should().Be("A message is required. | A message must be short.");
    }

    [Fact]
    public async Task TryHandleAsync_WhenUnexpectedFailure_ShouldKeepErrorSafeAndMatchingStatus()
    {
        // Arrange
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var stream = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = stream;
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        // Act
        await handler.TryHandleAsync(context, new InvalidOperationException("private-provider-body"), CancellationToken.None);
        stream.Position = 0;
        using var body = await JsonDocument.ParseAsync(stream);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        body.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("message").GetString().Should().Be("An unexpected error occurred");
        body.RootElement.GetProperty("data").GetProperty("statusCode").GetInt32().Should().Be(500);
        body.RootElement.GetRawText().Should().NotContain("private-provider-body");
    }
}
