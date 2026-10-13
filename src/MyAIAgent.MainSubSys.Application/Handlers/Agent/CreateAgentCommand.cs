using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Interfaces;

namespace MyAIAgent.MainSubSys.Application.Handlers.Agent;

public sealed record CreateAgentCommand(
    string Name,
    string Instructions,
    string? McpServerEndpoint,
    bool IsDefault,
    IReadOnlyList<string>? EnabledTools = null)
    : IRequest<ResponseModel<string>>;
