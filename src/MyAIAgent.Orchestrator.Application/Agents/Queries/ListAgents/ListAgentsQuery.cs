/**
 * MyAIAgent | Orchestrator · Application
 *
 * Carries agent listing input through the Orchestrator MediatR application boundary.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Agents.Queries.ListAgents;

public sealed record ListAgentsQuery : IRequest<ResponseModel<IReadOnlyList<AgentSummary>>>;
