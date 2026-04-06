using MediatR;
using MyAIAgent.Common.Domain.Models;
using MyAIAgent.Orchestrator.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Handlers.Agents;

public sealed record ListAgentsQuery : IRequest<ResponseModel<IReadOnlyList<AgentSummary>>>;
