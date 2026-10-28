/**
 * MyAIAgent | Orchestrator · Application
 *
 * Carries agent deletion input through the Orchestrator MediatR application boundary.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.Orchestrator.Application.Agents.Commands.DeleteAgent;

public sealed record DeleteAgentCommand(string AgentId) : IRequest<ResponseModel<bool>>;
