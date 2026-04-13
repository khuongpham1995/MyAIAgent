using MyAIAgent.MainSubSys.Application.Handlers.Chat;
using MyAIAgent.MainSubSys.Application.Models;

namespace MyAIAgent.MainSubSys.Application.Mappers;

public static class ChatGatewayCommandMapper
{
    public static ChatGatewayCommand ToCommand(this ChatGatewayRequestDto dto) =>
        new(dto.SessionId, dto.UserMessage, dto.AgentId);
}
