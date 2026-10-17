using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Agents.Commands.DeleteAgent;

public sealed record DeleteAgentCommand(string AgentId) : IRequest<ResponseModel<bool>>;
