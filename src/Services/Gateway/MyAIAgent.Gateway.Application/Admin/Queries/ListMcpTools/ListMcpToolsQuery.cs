/**
 * MyAIAgent | Gateway · Application
 *
 * Carries MCP tool discovery input through the Gateway MediatR application boundary.
 */

using MyAIAgent.Common.Application.Models;
using MediatR;

namespace MyAIAgent.Gateway.Application.Admin.Queries.ListMcpTools;

public sealed record ListMcpToolsQuery : IRequest<ResponseModel<IReadOnlyList<ToolInfo>>>;
