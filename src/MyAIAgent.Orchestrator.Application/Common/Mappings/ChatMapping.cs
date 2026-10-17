using Mapster;
using MyAIAgent.Orchestrator.Application.Chat.Commands.Chat;
using MyAIAgent.Orchestrator.Application.Models;

namespace MyAIAgent.Orchestrator.Application.Common.Mappings;

public sealed class ChatMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ChatRequestDto, ChatCommand>()
            .MapWith(source => new ChatCommand(
                source.SessionId,
                source.UserMessage,
                source.UserId,
                source.AgentId));
    }
}
