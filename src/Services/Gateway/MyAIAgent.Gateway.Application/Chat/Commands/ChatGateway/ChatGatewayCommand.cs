/**
 * MyAIAgent | Gateway · Application
 *
 * Carries ordinary chat forwarding input through the Gateway MediatR application boundary.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.Gateway.Application.Chat.Commands.ChatGateway;

public sealed record ChatGatewayCommand(string? SessionId, string UserMessage, string UserId, string? AgentId = null)
    : IRequest<ResponseModel<ChatGatewayResponse>>;
