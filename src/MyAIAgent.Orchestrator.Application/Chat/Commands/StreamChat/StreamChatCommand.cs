using MediatR;

namespace MyAIAgent.Orchestrator.Application.Chat.Commands.StreamChat;

public sealed record StreamChatCommand(string? SessionId, string UserMessage, string UserId, string? AgentId = null)
    : IStreamRequest<ChatStreamEvent>;
