using MediatR;
using MyAIAgent.Gateway.Application.Common.Interfaces;

namespace MyAIAgent.Gateway.Application.Chat.Commands.StreamChatGateway;

public sealed class StreamChatGatewayHandler(IOrchestratorStreamClient streamClient)
    : IStreamRequestHandler<StreamChatGatewayCommand, ChatStreamEvent>
{
    public IAsyncEnumerable<ChatStreamEvent> Handle(
        StreamChatGatewayCommand command,
        CancellationToken cancellationToken) =>
        streamClient.StreamChatAsync(
            command.SessionId,
            command.UserMessage,
            command.UserId,
            command.AgentId,
            cancellationToken);
}
