using FluentValidation;
using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Application.Interfaces;

namespace MyAIAgent.Orchestrator.Application.Handlers.Agents;

public sealed record DeleteAgentCommand(string AgentId) : IRequest<ResponseModel<bool>>;
