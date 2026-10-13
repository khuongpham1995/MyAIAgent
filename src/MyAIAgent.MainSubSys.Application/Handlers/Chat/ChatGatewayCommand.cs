using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Interfaces;
using MyAIAgent.MainSubSys.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Handlers.Chat;

public sealed record ChatGatewayCommand(string? SessionId, string UserMessage, string UserId, string? AgentId = null)
    : IRequest<ResponseModel<ChatGatewayResponse>>;
