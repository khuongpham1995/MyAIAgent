/**
 * MyAIAgent | Gateway · Application
 *
 * Carries agent listing input through the Gateway MediatR application boundary.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.Gateway.Application.Agents.Queries.ListAgents;

public sealed record ListAgentsQuery : IRequest<ResponseModel<IReadOnlyList<AgentSummary>>>;
