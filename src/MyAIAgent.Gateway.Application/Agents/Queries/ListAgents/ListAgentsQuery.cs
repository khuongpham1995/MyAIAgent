/**
 * MyAIAgent | Gateway · Application
 *
 * Carries agent listing input through the Gateway MediatR application boundary.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Gateway.Application.Agents.Queries.ListAgents;

public sealed record ListAgentsQuery : IRequest<ResponseModel<IReadOnlyList<AgentSummary>>>;
