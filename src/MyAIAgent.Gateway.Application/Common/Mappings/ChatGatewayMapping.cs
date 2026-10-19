using Mapster;
using MyAIAgent.Gateway.Application.Chat.Commands.ChatGateway;
using MyAIAgent.Gateway.Application.Chat.Commands.StreamChatGateway;
using MyAIAgent.Gateway.Application.Chat;

namespace MyAIAgent.Gateway.Application.Common.Mappings;

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

        config.NewConfig<AuthenticatedChatGatewayRequest, StreamChatGatewayCommand>()
            .MapWith(source => new StreamChatGatewayCommand(
                source.Request.SessionId,
                source.Request.UserMessage,
                source.UserId,
                source.Request.AgentId));

        config.NewConfig<PublicChatGatewayRequest, StreamChatGatewayCommand>()
            .MapWith(source => new StreamChatGatewayCommand(
                source.Request.SessionId,
                source.Request.UserMessage,
                source.UserId,
                source.AgentId));
    }
}
