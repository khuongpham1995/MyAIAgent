using MyAIAgent.Orchestrator.Application.Handlers;
using MyAIAgent.Orchestrator.Application.Models;

namespace MyAIAgent.Orchestrator.Application.Mappers;

public static class ChatCommandMapper
{
    public static ChatCommand ToCommand(this ChatRequestDto chatRequestDto)
        => new(chatRequestDto.SessionId, chatRequestDto.UserMessage, chatRequestDto.UserId, chatRequestDto.AgentId);
}
