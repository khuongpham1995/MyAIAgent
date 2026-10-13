using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.MainSubSys.Application.Interfaces;
using MyAIAgent.MainSubSys.Domain.Models;

namespace MyAIAgent.MainSubSys.Application.Handlers.Agent;

public sealed record ListAgentsQuery : IRequest<ResponseModel<IReadOnlyList<AgentSummary>>>;
