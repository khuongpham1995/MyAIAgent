/**
 * MyAIAgent | Gateway · Application
 *
 * Carries streaming chat forwarding input through the Gateway MediatR application boundary.
 */

using MediatR;

namespace MyAIAgent.Gateway.Application.Chat.Commands.StreamChatGateway;

public sealed record StreamChatGatewayCommand(string? SessionId, string UserMessage, string UserId, string? AgentId = null)
    : IStreamRequest<ChatStreamEvent>;
