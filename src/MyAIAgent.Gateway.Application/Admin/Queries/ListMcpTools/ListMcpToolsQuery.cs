/**
 * MyAIAgent | Gateway · Application
 *
 * Carries MCP tool discovery input through the Gateway MediatR application boundary.
 */

using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Gateway.Application.Admin.Queries.ListMcpTools;

public sealed record ListMcpToolsQuery : IRequest<ResponseModel<IReadOnlyList<ToolInfo>>>;
