using MyAIAgent.Orchestrator.Application.Handlers;
using MyAIAgent.Orchestrator.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Mappers;

public static class ChatCommandMapper
{
    public static ChatCommand ToCommand(this ChatRequestDto chatRequestDto)
    {
        return new ChatCommand(chatRequestDto.SessionId, chatRequestDto.UserMessage);
    }
}