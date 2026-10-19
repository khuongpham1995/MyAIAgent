using MediatR;
using MyAIAgent.Common.Domain.Models;

namespace MyAIAgent.Gateway.Application.Admin.Queries.ListMcpTools;

public sealed record ListMcpToolsQuery : IRequest<ResponseModel<IReadOnlyList<ToolInfo>>>;
