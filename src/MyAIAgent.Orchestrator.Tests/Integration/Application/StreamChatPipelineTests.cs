using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MyAIAgent.Common.Application.Behaviors;
using MyAIAgent.Orchestrator.Application.Chat;
using MyAIAgent.Orchestrator.Application.Chat.Commands.StreamChat;

namespace MyAIAgent.Orchestrator.Tests.Integration.Application;

public sealed class StreamChatPipelineTests
{
    [Fact]
    public async Task CreateStream_WhenMessageIsEmpty_ShouldThrowValidationExceptionBeforeHandlerRuns()
    {
        // Arrange
        var handler = new Mock<IStreamRequestHandler<StreamChatCommand, ChatStreamEvent>>();
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<StreamChatCommand>());
        services.AddTransient(typeof(IStreamPipelineBehavior<,>), typeof(StreamValidationBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<StreamChatCommandValidator>();
        services.AddSingleton(handler.Object);
        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var act = async () =>
        {
            await foreach (var _ in sender.CreateStream(new StreamChatCommand(null, "", "visitor:a")))
            {
            }
        };

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        handler.Verify(
            h => h.Handle(It.IsAny<StreamChatCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
