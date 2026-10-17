using Mapster;
using MyAIAgent.MainSubSys.Application.Chat.Commands.ChatGateway;
using MyAIAgent.MainSubSys.Application.Models;

namespace MyAIAgent.MainSubSys.Application.Common.Mappings;

public sealed class ChatGatewayMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AuthenticatedChatGatewayRequest, ChatGatewayCommand>()
            .MapWith(source => new ChatGatewayCommand(
                source.Request.SessionId,
                source.Request.UserMessage,
                source.UserId,
                source.Request.AgentId));
    }
}
