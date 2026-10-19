using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Gateway.Application.Agents.Commands.DeleteAgent;

public sealed record DeleteAgentCommand(string AgentId) : IRequest<ResponseModel<bool>>;
