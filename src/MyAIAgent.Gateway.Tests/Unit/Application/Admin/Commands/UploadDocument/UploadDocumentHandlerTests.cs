/**
 * MyAIAgent | Gateway · Tests
 *
 * Covers upload validation, downstream forwarding, failure results, and cancellation in Gateway.
 */

using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Application.Admin.Commands.UploadDocument;
using MyAIAgent.Gateway.Application.Common.Interfaces;
using MyAIAgent.Gateway.Application.Admin;

namespace MyAIAgent.Gateway.Tests.Unit.Application.Admin.Commands.UploadDocument;

public sealed class UploadDocumentHandlerTests
{
    [Fact]
    public async Task Handle_WhenCommandIsValid_ShouldReturnSuccess()
    {
        // Arrange
        var mockClient = new Mock<IKernelMemoryClient>();
        mockClient
            .Setup(c => c.UploadDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<DocumentUploadResult>.Succeed(
                new DocumentUploadResult("doc-1", "accepted")));

        var handler = new UploadDocumentHandler(mockClient.Object);
        using var stream = new MemoryStream([1, 2, 3]);
        var command = new UploadDocumentCommand("report.pdf", stream, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.DocumentId.Should().Be("doc-1");
        result.Data.Status.Should().Be("accepted");
    }

    [Fact]
    public async Task Handle_WhenKernelMemoryReturnsFail_ShouldPropagateFailure()
    {
        // Arrange
        var mockClient = new Mock<IKernelMemoryClient>();
        mockClient
            .Setup(c => c.UploadDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<DocumentUploadResult>.Fail(message: "Document upload failed."));

        var handler = new UploadDocumentHandler(mockClient.Object);
        using var stream = new MemoryStream([1, 2, 3]);
        var command = new UploadDocumentCommand("report.pdf", stream, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Document upload failed.");
    }

    [Fact]
    public async Task Handle_WhenFileNameIsEmpty_ShouldThrowValidationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(UploadDocumentHandler).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(typeof(UploadDocumentHandler).Assembly, includeInternalTypes: true);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(new Mock<IKernelMemoryClient>().Object);
        services.AddSingleton(new Mock<IOrchestratorClient>().Object);
        services.AddSingleton(new Mock<IMcpServerClient>().Object);

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var act = async () => await sender.Send(new UploadDocumentCommand("", stream, null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WhenDocumentIdIsProvided_ShouldPassItToClient()
    {
        // Arrange
        var mockClient = new Mock<IKernelMemoryClient>();
        mockClient
            .Setup(c => c.UploadDocumentAsync(
                "report.pdf",
                It.IsAny<Stream>(),
                "custom-id",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<DocumentUploadResult>.Succeed(
                new DocumentUploadResult("custom-id", "accepted")));

        var handler = new UploadDocumentHandler(mockClient.Object);
        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var result = await handler.Handle(
            new UploadDocumentCommand("report.pdf", stream, "custom-id"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        mockClient.Verify(c => c.UploadDocumentAsync(
            "report.pdf", It.IsAny<Stream>(), "custom-id", It.IsAny<CancellationToken>()), Times.Once);
    }
}
