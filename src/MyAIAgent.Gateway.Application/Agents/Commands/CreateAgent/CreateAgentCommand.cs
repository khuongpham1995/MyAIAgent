/**
 * MyAIAgent | Gateway · Application
 *
 * Carries agent creation input through the Gateway MediatR application boundary.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Gateway.Application.Agents.Commands.CreateAgent;

public sealed record CreateAgentCommand(
    string Name,
    string Instructions,
    string? McpServerEndpoint,
    bool IsDefault,
    IReadOnlyList<string>? EnabledTools = null)
    : IRequest<ResponseModel<string>>;
