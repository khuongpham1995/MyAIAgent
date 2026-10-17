using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Common.Interfaces;

namespace MyAIAgent.MainSubSys.Application.Chat.Commands.ChatGateway;

public sealed class ChatGatewayHandler(IOrchestratorClient orchestratorClient)
    : IRequestHandler<ChatGatewayCommand, ResponseModel<ChatGatewayResponse>>
{
    public async Task<ResponseModel<ChatGatewayResponse>> Handle(
        ChatGatewayCommand command,
        CancellationToken cancellationToken) =>
        await orchestratorClient.ChatAsync(
            command.SessionId,
            command.UserMessage,
            command.UserId,
            command.AgentId,
            cancellationToken);
}
