/**
 * MyAIAgent | Orchestrator · Application
 *
 * Carries agent deletion input through the Orchestrator MediatR application boundary.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Orchestrator.Application.Agents.Commands.DeleteAgent;

public sealed record DeleteAgentCommand(string AgentId) : IRequest<ResponseModel<bool>>;
