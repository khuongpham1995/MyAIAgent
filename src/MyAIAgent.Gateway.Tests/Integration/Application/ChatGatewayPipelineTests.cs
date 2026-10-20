/**
 * MyAIAgent | Gateway · Tests
 *
 * Checks that the Gateway MediatR pipeline rejects invalid chat before invoking downstream dependencies.
 */

using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Gateway.Application.Chat.Commands.ChatGateway;
using MyAIAgent.Gateway.Application.Common.Interfaces;
using MyAIAgent.Gateway.Application.Chat;

namespace MyAIAgent.Gateway.Tests.Integration.Application;

public sealed class ChatGatewayPipelineTests
{
    private static ISender BuildSender(IOrchestratorClient orchestratorClient)
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ChatGatewayHandler).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(typeof(ChatGatewayHandler).Assembly, includeInternalTypes: true);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(orchestratorClient);
        services.AddSingleton(new Mock<IKernelMemoryClient>().Object);
        services.AddSingleton(new Mock<IMcpServerClient>().Object);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Handle_WhenValidCommandDispatchedThroughFullPipeline_ShouldReturnSuccess()
    {
        // Arrange
        var mockClient = new Mock<IOrchestratorClient>();
        mockClient
            .Setup(c => c.ChatAsync(null, "Hello pipeline", "tenant:user", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseModel<ChatGatewayResponse>.Succeed(
                new ChatGatewayResponse("Pipeline reply", "pipeline-session")));

        var sender = BuildSender(mockClient.Object);

        // Act
        var result = await sender.Send(new ChatGatewayCommand(null, "Hello pipeline", "tenant:user"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Reply.Should().Be("Pipeline reply");
        result.Data.SessionId.Should().Be("pipeline-session");
    }

    [Fact]
    public async Task Handle_WhenUserMessageExceedsMaxLength_ShouldThrowValidationException()
    {
        // Arrange
        var sender = BuildSender(new Mock<IOrchestratorClient>().Object);
        var longMessage = new string('x', 4001);

        // Act
        var act = async () => await sender.Send(new ChatGatewayCommand(null, longMessage, "tenant:user"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*4000*");
    }

    [Fact]
    public async Task Handle_WhenUserMessageIsWhitespace_ShouldThrowValidationException()
    {
        // Arrange
        var sender = BuildSender(new Mock<IOrchestratorClient>().Object);

        // Act
        var act = async () => await sender.Send(new ChatGatewayCommand(null, "   ", "tenant:user"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
