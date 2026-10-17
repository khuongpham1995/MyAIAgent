using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Agents.Queries.ListAgents;

public sealed record ListAgentsQuery : IRequest<ResponseModel<IReadOnlyList<AgentSummary>>>;
