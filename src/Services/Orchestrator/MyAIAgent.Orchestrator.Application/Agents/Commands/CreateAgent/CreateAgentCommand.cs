/**
 * MyAIAgent | Orchestrator · Application
 *
 * Carries agent creation input through the Orchestrator MediatR application boundary.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.Orchestrator.Application.Agents.Commands.CreateAgent;

public sealed record CreateAgentCommand(
    string Name,
    string Instructions,
    string McpServerEndpoint,
    bool IsDefault,
    IReadOnlyList<string>? EnabledTools = null)
    : IRequest<ResponseModel<string>>;
