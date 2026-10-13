using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Interfaces;

namespace MyAIAgent.MainSubSys.Application.Handlers.Agent;

public sealed record DeleteAgentCommand(string AgentId) : IRequest<ResponseModel<bool>>;
